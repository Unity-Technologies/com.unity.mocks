using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
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
            var buildFolder = new NPath(GetType().Assembly.Location).Parent;
            buildFolder.CopyFiles(BaseDir, true, f => f.ExtensionWithDot == ".dll");

            MockInjector = new MockInjector(BaseDir);
        }

        [OneTimeTearDown]
        public void OneTimeTearDownPatching() => MockInjector.Dispose();

        protected NPath Compile(string testAssemblyName, string sourceCode, params string[] dependentAssemblyNames)
        {
            // prefix the assembly name because they are globally unique and don't want to ever collide
            var testAssemblyPath = BaseDir
                .Combine($"{TestContext.CurrentContext.GetFixtureName()}_{testAssemblyName}")
                .ChangeExtension(".dll");

            // set up to compile

            var compiler = new Microsoft.CSharp.CSharpCodeProvider();
            var compilerArgs = new CompilerParameters
            {
                OutputAssembly = testAssemblyPath,
                IncludeDebugInformation = true,
                CompilerOptions = "/o- /debug+ /warn:0"
            };
            compilerArgs.ReferencedAssemblies.Add(typeof(int).Assembly.Location); // mscorlib

            // TODO: use typecache
            var assemblies = AppDomain.CurrentDomain
                .GetAssemblies()
                .Where(a => !a.IsDynamic)
                .ToDictionary(a => a.GetName().Name, a => a.Location.ToNPath(), StringComparer.OrdinalIgnoreCase);
            
            foreach (var dependentAssemblyName in dependentAssemblyNames)
            {
                // we may have already copied it in
                var path = BaseDir.Combine(dependentAssemblyName).ChangeExtension(".dll");
                
                // if not,  
                if (!path.Exists() && assemblies.TryGetValue(dependentAssemblyName, out path))
                    path.Copy(BaseDir.Combine(path.FileName));

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
