using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using NUnit.Framework;

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
                using NSubstitute.Elevated;
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
                    "SystemUnderTest",
                    "NSubstitute", "NSubstitute.Elevated", "NUnit.Framework",
                    "System.Threading.Tasks.Extensions", "System.Runtime");

            // use testAssembly to scan for uses of Substitute.For and retrieve System.DateTime.Now and Class.Add 
            
            using (var systemUnderTest = AssemblyDefinition.ReadAssembly(systemUnderTestPath))
            using (var mscorlib = systemUnderTest.MainModule.AssemblyResolver.Resolve(systemUnderTest.MainModule.AssemblyReferences.SingleOrDefault(c => c.Name == "mscorlib")))
            {
                // list of all stuff we detect an NSub extension called on it
                var mockedMethodDefinitions = new[]
                {
                    mscorlib.MainModule.GetType("System.DateTime").Properties.Single(p => p.Name == "Now").GetMethod,
                    systemUnderTest.MainModule.GetType("Class").Methods.Single(m => m.Name == "Add"),
                };

                Patch(mockedMethodDefinitions);
            }
        }

        void Patch(IEnumerable<MethodDefinition> methodsToPatch)
        {
            foreach (var methodToPatch in methodsToPatch)
                Patch(methodToPatch);
        }

        void Patch(MethodDefinition methodToPatch)
        {
                        
        }
    }
}
