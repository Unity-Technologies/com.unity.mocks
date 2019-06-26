using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Unity.Utils;

namespace NSubstitute.Weaver
{
    [Flags]
    public enum PatchOptions
    {
        PatchTestAssembly   = 1 << 0,   // typically we don't want to patch the test assembly itself, only the systems under test
        SkipPeVerify        = 1 << 1,   // maybe flip this bit the other way when we get a really solid weaver (peverify has an obvious perf cost)
    }

    public static class ElevatedWeaver
    {
        const string k_PatchBackupExtension = ".orig";

        public static string GetPatchBackupPathFor(string path)
        => path + k_PatchBackupExtension;

        public static IReadOnlyCollection<PatchResult> PatchAllDependentAssemblies(NPath testAssemblyPath, PatchOptions patchOptions)
        {
            // TODO: ensure we do not have any assemblies that we want to patch already loaded
            // (this will require the separate in-memory patching ability)

            // this dll has types we're going to be injecting, so ensure it is in the same folder
            //var targetWeaverDll

            var toProcess = new List<NPath> { testAssemblyPath.FileMustExist() };
            var patchResults = new Dictionary<string, PatchResult>(StringComparer.OrdinalIgnoreCase);
            var mockInjector = new MockInjector(testAssemblyPath.Parent.Combine("NSubstitute.Elevated"));
            
            for (var toProcessIndex = 0; toProcessIndex < toProcess.Count; ++toProcessIndex)
            {
                var assemblyToPatchPath = toProcess[toProcessIndex];

                // as we accumulate dependencies recursively, we will probably get some duplicates we can early-out on
                if (patchResults.ContainsKey(assemblyToPatchPath))
                    continue;

                using (var assemblyToPatch = AssemblyDefinition.ReadAssembly(assemblyToPatchPath))
                {
                    GatherReferences(assemblyToPatchPath, assemblyToPatch);

                    var patchResult = TryPatch(assemblyToPatchPath, assemblyToPatch);
                    patchResults.Add(assemblyToPatchPath, patchResult);
                }
            }

            void GatherReferences(NPath assemblyToPatchPath, AssemblyDefinition assemblyToPatch)
            {
                foreach (var referencedAssembly in assemblyToPatch.Modules.SelectMany(m => m.AssemblyReferences))
                {
                    // only patch dll's we "own", that are in the same folder as the test assembly
                    var referencedAssemblyPath = assemblyToPatchPath.Parent.Combine(referencedAssembly.Name + ".dll");

                    if (referencedAssemblyPath.FileExists())
                        toProcess.Add(referencedAssemblyPath);
                    else if (!patchResults.ContainsKey(referencedAssembly.Name))
                        patchResults.Add(referencedAssembly.Name, new PatchResult(referencedAssembly.Name, null, PatchState.IgnoredForeignAssembly));
                }
            }

            PatchResult TryPatch(NPath assemblyToPatchPath, AssemblyDefinition assemblyToPatch)
            {
                var alreadyPatched = mockInjector.IsPatched(assemblyToPatch);
                var cannotPatch = assemblyToPatch.Name.HasPublicKey;

                if (assemblyToPatchPath == testAssemblyPath && (patchOptions & PatchOptions.PatchTestAssembly) == 0)
                {
                    if (alreadyPatched)
                        throw new Exception("Unexpected already-patched test assembly, yet we want PatchTestAssembly.No");
                    if (cannotPatch)
                        throw new Exception("Cannot patch an assembly with a strong name");
                    return new PatchResult(assemblyToPatchPath, null, PatchState.IgnoredTestAssembly);
                }

                if (alreadyPatched)
                    return new PatchResult(assemblyToPatchPath, null, PatchState.AlreadyPatched);
                if (cannotPatch)
                    return new PatchResult(assemblyToPatchPath, null, PatchState.IgnoredForeignAssembly);
                
                return Patch(assemblyToPatchPath, assemblyToPatch);
            }

            PatchResult Patch(NPath assemblyToPatchPath, AssemblyDefinition assemblyToPatch)
            {
                mockInjector.Patch(assemblyToPatch);

                // atomic write of file with backup
                // TODO: skip backup if existing file already patched. want the .orig to only be the unpatched file.

                // write to tmp and release the lock
                var tmpPath = assemblyToPatchPath.ChangeExtension(".tmp");
                tmpPath.DeleteIfExists();
                assemblyToPatch.Write(tmpPath); // $$$ , new WriterParameters { WriteSymbols = true }); see https://github.com/jbevain/cecil/issues/421
                assemblyToPatch.Dispose();

                if ((patchOptions & PatchOptions.SkipPeVerify) == 0)
                    PeVerify.Verify(tmpPath);

                // move the actual file to backup, and move the tmp to actual
                var backupPath = GetPatchBackupPathFor(assemblyToPatchPath);
                File.Replace(tmpPath, assemblyToPatchPath, backupPath);

                // TODO: move pdb file too

                return new PatchResult(assemblyToPatchPath, backupPath, PatchState.Patched);
            }

            return patchResults.Values;
        }
    }

    public enum PatchState
    {
        GeneralFailure,            // something else went wrong
        IgnoredTestAssembly,       // don't patch the test assembly itself, as we're requiring that to always be separate from the systems under test
        IgnoredForeignAssembly,    // don't want to patch things that are not "ours"
        //AlreadyPatchedOld,       // assy already patched against an older set of tooling TODO: implement
        AlreadyPatched,            // assy already patched against current tooling
        Patched,                   // assy patched and old one backed up
    }

    public struct PatchResult
    {
        public string Path;
        public string OriginalPath;
        public PatchState PatchState;

        [DebuggerStepThrough]
        public PatchResult(string path, string originalPath, PatchState patchState)
        {
            Path = path;
            OriginalPath = originalPath;
            PatchState = patchState;
        }
    }
}
