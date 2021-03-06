using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using NSubstitute.Weaver;
using NUnit.Framework;
using Unity.Utils;

namespace NSubstitute.Elevated.Tests
{
    class NewTests : PatchingFixture
    {
        [Test]
        public void BasicTest()
        {
            var assemblyPath = Assembly.GetExecutingAssembly().Location.ToNPath().Parent;
            var systemUnderTestPath = assemblyPath.Combine("Minimal.Systems.dll"); 
            var testPath = assemblyPath.Combine("Minimal.Tests.dll"); 

            // TODO: use testAssembly to scan for uses of Substitute.For and retrieve System.DateTime.Now and Class.Add 
            
            using (var systemUnderTest = AssemblyDefinition.ReadAssembly(systemUnderTestPath))
            using (var mscorlib = AssemblyDefinition.ReadAssembly(typeof(object).Assembly.Location))
            {
                // list of all stuff we detect an NSub extension called on it
                var mockedMethodDefinitions = new[]
                {
                    mscorlib.MainModule.GetType("System.DateTime").Properties.Single(p => p.Name == "Now").GetMethod,
                    systemUnderTest.MainModule.GetType("Inner").Methods.Single(m => m.Name == "Add"),
                };

                Patch(mockedMethodDefinitions);

                Write(systemUnderTest, systemUnderTestPath);
                Write(mscorlib, BaseDir.Combine("mscorlib.dll"));
            }

            // would like to do this in an appdomain but complicated
            using (ElevatedSubstitutionContext.AutoHook())
            {
                var testAssembly = Assembly.LoadFile(testPath);
                var fixtureType = testAssembly.GetType("Fixture", true);
                var fixture = Activator.CreateInstance(fixtureType);
                var mockMethod = fixtureType.GetMethod("Mock");
                mockMethod.Invoke(fixture, Array.Empty<object>());
            }
        }

        void Patch(IEnumerable<MethodDefinition> methodsToPatch)
        {
            MockInjector.Patch(methodsToPatch);
        }

        void Write(AssemblyDefinition assemblyToPatch, NPath assemblyToPatchPath, PatchOptions patchOptions = default)
        {
            // atomic write of file with backup
            // TODO: skip backup if existing file already patched. want the .orig to only be the unpatched file.

            // write to tmp and release the lock
            var tmpPath = assemblyToPatchPath.ChangeExtension(".tmp");
            tmpPath.DeleteIfExists();
            assemblyToPatch.Write(tmpPath); // $$$ , new WriterParameters { WriteSymbols = true }); see https://github.com/jbevain/cecil/issues/421
            assemblyToPatch.Dispose();

            // TODO: peverify obviously won't work with memory written streams. also needs all the dependencies.
            // solution is to create a temp folder, write the patched dll there as well as any of its direct dependencies, and run peverify
            
            if ((patchOptions & PatchOptions.SkipPeVerify) == 0)
                PeVerify.Verify(tmpPath);

            // move the actual file to backup, and move the tmp to actual
            var backupPath = ElevatedWeaver.GetPatchBackupPathFor(assemblyToPatchPath);
            File.Replace(tmpPath, assemblyToPatchPath, backupPath);
        }
    }
}
