using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            const string k_SystemUnderTestCode = @"
                public class Class
                {
                    public int Add(int a, int b) => a + b;
                }

                public class System
                {
                    Class _class;
                    int _a, _b;

                    public System(Class c, int a, int b)
                    {
                        _class = c;
                        _a = a;
                        _b = b;
                    }

                    public int AddIt() => _class.Add(_a, _b);
                    public int Mul() => _a * _b;
                }
                ";

            const string k_TestCode = @"
                using System;
                using NSubstitute;
                using NUnit.Framework;

                class Fixture
                {
                    [Test]
                    public void Mock()
                    {
                        using (SubstituteStatic.For<DateTime>())
                        {
                            var today = DateTime.Today;
                            DateTime.Now.Returns(today);

                            var c = Substitute.For<Class>();
                            c.Add(1, 2).Returns(5);
                            
                            Assert.Equals(today, DateTime.Now);
                            Assert.Equals(3, c.Add(2, 1));
                            Assert.Equals(5, c.Add(1, 2));
                        }
                    }
                }
                ";
            
            var systemUnderTestPath = Compile("SystemUnderTest", k_SystemUnderTestCode);
            var testPath = Compile("Test", k_TestCode,
                    systemUnderTestPath.FileNameWithoutExtension,
                    "NSubstitute", "NSubstitute.Elevated", "NUnit.Framework", "System.Runtime",
                    "System.Threading.Tasks.Extensions", "Castle.Core", "netstandard", "mscorlib");

            // use testAssembly to scan for uses of Substitute.For and retrieve System.DateTime.Now and Class.Add 
            
            using (var systemUnderTest = AssemblyDefinition.ReadAssembly(systemUnderTestPath))
            using (var mscorlib = AssemblyDefinition.ReadAssembly(BaseDir.Combine("mscorlib.dll")))
            {
                // list of all stuff we detect an NSub extension called on it
                var mockedMethodDefinitions = new[]
                {
                    mscorlib.MainModule.GetType("System.DateTime").Properties.Single(p => p.Name == "Now").GetMethod,
                    systemUnderTest.MainModule.GetType("Class").Methods.Single(m => m.Name == "Add"),
                };

                Patch(mockedMethodDefinitions);

                Write(systemUnderTest, systemUnderTestPath);
                Write(mscorlib, BaseDir.Combine("mscorlib.dll"));
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

            if ((patchOptions & PatchOptions.SkipPeVerify) == 0)
                PeVerify.Verify(tmpPath);

            // move the actual file to backup, and move the tmp to actual
            var backupPath = ElevatedWeaver.GetPatchBackupPathFor(assemblyToPatchPath);
            File.Replace(tmpPath, assemblyToPatchPath, backupPath);
        }
    }
}
