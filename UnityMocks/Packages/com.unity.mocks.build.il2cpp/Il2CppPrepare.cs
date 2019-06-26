using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Mono.Cecil;
using NSubstitute.Elevated;
using Unity.Mocks.Build;
using UnityEditor;
using UnityEngine;
using UnityEditor.TestTools;
using UnityEngine.TestTools;
using OpCodes = Mono.Cecil.Cil.OpCodes;

[assembly: TestPlayerBuildModifier(typeof(Il2CppPrepare))]
[assembly: PostBuildCleanup(typeof(Il2CppPrepare))]

namespace Unity.Mocks.Build
{
    public class Il2CppPrepare : ITestPlayerBuildModifier
    {
        public BuildPlayerOptions ModifyOptions(BuildPlayerOptions playerOptions)
        {
            var testAssemblies = GetTestAssemblies();
            foreach (var ta in testAssemblies)
            {
                Debug.Log($"Prepare assembly for patching {ta.Location}");
                var types = GetAllMockedStaticTypes(ta.Location);
                foreach (var t in types)
                {
                    Debug.Log($"Mocked type {t.FullName}");
                }
            }
            return playerOptions;
        }

        private static IEnumerable<TypeDefinition> GetAllMockedStaticTypes(string localtion)
        {
            var assembly = AssemblyDefinition.ReadAssembly(localtion);
            var allTestMethods = GetAllTestMethods(assembly);
            foreach (var m in allTestMethods)
            {
                var mockingCalls = GetStaticMockCalls(m);
                foreach (var mc in mockingCalls)
                {
                    var pt = ((GenericInstanceMethod)mc).GenericArguments[0];
                    yield return pt.Resolve();
                }
            }
        }
        private static IEnumerable GetStaticMockCalls(MethodDefinition method)
        {
            return method.Body.Instructions
                .Where(instruction => instruction.OpCode == OpCodes.Call)
                .Select(instruction => (MethodReference)instruction.Operand)
                .Where(methodReference => methodReference.FullName.Contains("NSubstitute.Elevated.Substitute::For"));
        }

        private static IEnumerable<MethodDefinition> GetAllTestMethods(AssemblyDefinition assembly)
        {
            var result = new List<MethodDefinition>();
            foreach (var module in assembly.Modules)
            {
                foreach (var t in module.Types)
                {
                    result.AddRange(t.Methods.Where(m => m.HasCustomAttributes));
                }
            }
            return result;
        }


        static IEnumerable<Assembly> GetTestAssemblies()
        {
            var testAssemblies = AppDomain.CurrentDomain.GetAssemblies().Where(x => x.GetReferencedAssemblies().Any(ra => ra.Name == "nunit.framework"));
            testAssemblies = testAssemblies.Where(x => x.GetName().Name == "PlaymodeTests");
            return testAssemblies;
        }
    }
}
