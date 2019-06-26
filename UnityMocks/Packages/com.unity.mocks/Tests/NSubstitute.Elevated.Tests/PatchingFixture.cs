using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using NSubstitute.Elevated.Internals;
using NSubstitute.Weaver;
using NUnit.Framework;
using Shouldly;
using Unity.Mocks.Tests;
using Unity.Utils;

namespace NSubstitute.Elevated.Tests
{
    abstract class PatchingFixture : TestFileSystemFixture
    {
        protected MockInjector MockInjector { get; private set; }
        
        [OneTimeSetUp]
        public void OneTimeSetUpPatching()
        {
            // injector needs this
            var mockTypesAssembly = typeof(MockPlaceholderType).Assembly.Location.ToNPath();
            mockTypesAssembly.Copy(BaseDir);

            MockInjector = new MockInjector(BaseDir);
        }

        [OneTimeTearDown]
        public void OneTimeTearDownPatching() => MockInjector.Dispose();

        protected NPath Compile(string testAssemblyName, string sourceCode, params string[] dependentAssemblyNames)
        {
            // prefix the assembly name because they are globally unique and don't want to ever collide
            var testAssemblyPath = BaseDir
                .Combine($"{TestContext.CurrentContext.GetFixtureName()}_{testAssemblyName}.dll");

            // set up to compile

            var compiler = new Microsoft.CSharp.CSharpCodeProvider();
            var compilerArgs = new CompilerParameters
            {
                OutputAssembly = testAssemblyPath,
                IncludeDebugInformation = true,
                CompilerOptions = "/o- /debug+ /warn:0"
            };

            var searchPaths = new[]
            {
                NPath.CurrentDirectory.Combine("Library/ScriptAssemblies"),
                NPath.CurrentDirectory.Combine("Packages/nuget.nsubstitute"),
                typeof(object).Assembly.Location.ToNPath().Parent,
                typeof(object).Assembly.Location.ToNPath().Parent.Combine("Facades"),
                typeof(TestAttribute).Assembly.Location.ToNPath().Parent,
            };
            
            foreach (var dependentAssemblyFileName in dependentAssemblyNames.Select(n => n + ".dll"))
            {
                // we may have already copied it in
                var path = BaseDir.Combine(dependentAssemblyFileName);
                
                // if not, current appdomain ought to have it  
                if (!path.Exists())
                {
                    var srcPath = searchPaths
                        .Select(p => p.Combine(dependentAssemblyFileName))
                        .First(p => p.FileExists());
                    path = srcPath.Copy(BaseDir);
                }

                compilerArgs.ReferencedAssemblies.Add(path);
            }

            // compile and handle errors

            var compilerResult = compiler.CompileAssemblyFromSource(compilerArgs, sourceCode);
            if (compilerResult.Errors.Count > 0)
            {
                var errorText = compilerResult.Errors
                    .OfType<CompilerError>()
                    .Select(e => $"({e.Line},{e.Column}): error {e.ErrorNumber}: {e.ErrorText}")
                    .Prepend("Compiler errors:")
                    .StringJoin("\n");
                throw new Exception(errorText);
            }

            testAssemblyPath.ShouldBe(new NPath(compilerResult.PathToAssembly));

            PeVerify.Verify(testAssemblyPath); // sanity check on what the compiler generated

            return testAssemblyPath;
        }

        public TypeDefinition GetType(AssemblyDefinition testAssembly, string typeName)
            => testAssembly.MainModule.GetType(typeName);
        public IEnumerable<TypeDefinition> SelectTypes(AssemblyDefinition testAssembly, IncludeNested includeNested)
            => testAssembly.SelectTypes(includeNested);
    }
}
