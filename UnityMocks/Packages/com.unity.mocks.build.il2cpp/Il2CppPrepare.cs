using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Mocks.Build;
using UnityEditor;
using UnityEditor.TestTools;
using UnityEngine;
using UnityEngine.TestTools;

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
                // 
            }
            return playerOptions;
        }

        static IEnumerable<Assembly> GetTestAssemblies()
        {
            var testAssemblies = AppDomain.CurrentDomain.GetAssemblies().Where(x => x.GetReferencedAssemblies().Any(ra => ra.Name == "UnityEngine.TestRunner"));
            testAssemblies = testAssemblies.Where(x => x.GetName().Name != "UnityEditor.TestRunner");
            return testAssemblies;
        }
    }
}
