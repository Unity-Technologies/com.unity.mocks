using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using NSubstitute.Elevated.Internals;
using Unity.Utils;
using AssemblyMetadataAttribute = System.Reflection.AssemblyMetadataAttribute;

namespace NSubstitute.Weaver
{
    class MockInjector
    {
        readonly string k_MarkAsPatchedKey, k_MarkAsPatchedValue;
        readonly TypeDefinition k_MockPlaceholderType;
        readonly MethodDefinition k_PatchedAssemblyBridgeTryMock;

        public MockInjector(NPath assembliesDir)
        {
            var mockTypesPath = assembliesDir
                .DirectoryMustExist()
                .Combine(typeof(MockPlaceholderType).Assembly.Location.ToNPath().FileName);

            var assemblyBits = File.ReadAllBytes(mockTypesPath);
            using (var md5 = MD5.Create())
            {
                var hash = md5.ComputeHash(assemblyBits);
                k_MarkAsPatchedValue = hash.ToHexString();
            }

            using (var mockTypesAssembly = AssemblyDefinition.ReadAssembly(mockTypesPath))
            {
                k_MarkAsPatchedKey = mockTypesAssembly.Name.FullName;

                k_MockPlaceholderType = mockTypesAssembly.MainModule
                    .GetType(typeof(MockPlaceholderType).FullName);

                k_PatchedAssemblyBridgeTryMock = mockTypesAssembly.MainModule
                    .GetType(typeof(PatchedAssemblyBridge).FullName)
                    .Methods
                    .Single(m => m.Name == nameof(PatchedAssemblyBridge.TryMock));
            }
        }

        public void Patch(AssemblyDefinition assembly)
        {
            // patch all types

            var typesToProcess = assembly
                .SelectTypes(IncludeNested.Yes)
                .OrderBy(t => t.InheritanceChainLength())   // process base classes first
                .ToList();                                  // copy to a list in case patch work we do would invalidate the enumerator

            foreach (var type in typesToProcess)
                Patch(type);

            // add an attr to mark the assembly as patched

            var mainModule = assembly.MainModule;
            var types = mainModule.TypeSystem;

            var metadataAttrName = typeof(AssemblyMetadataAttribute);
            var metadataAttrType = new TypeReference(metadataAttrName.Namespace, metadataAttrName.Name, mainModule, types.CoreLibrary);
            var metadataAttrCtor = new MethodReference(".ctor", types.Void, metadataAttrType) { HasThis = true };
            metadataAttrCtor.Parameters.Add(new ParameterDefinition(types.String));
            metadataAttrCtor.Parameters.Add(new ParameterDefinition(types.String));

            var metadataAttr = new CustomAttribute(metadataAttrCtor);
            metadataAttr.ConstructorArguments.Add(new CustomAttributeArgument(types.String, k_MarkAsPatchedKey));
            metadataAttr.ConstructorArguments.Add(new CustomAttributeArgument(types.String, k_MarkAsPatchedValue));

            assembly.CustomAttributes.Add(metadataAttr);
        }

        public void Patch(IEnumerable<MethodDefinition> methodsToPatch)
        {
            foreach (var group in methodsToPatch.GroupBy(m => m.Module.Assembly))
                Patch(group.Key, group);
        }
        
        void Patch(AssemblyDefinition assembly, IEnumerable<MethodDefinition> methods)
        {
            foreach (var method in methods)
                Patch(method);

            // add an attr to mark the assembly as patched

            var mainModule = assembly.MainModule;
            var types = mainModule.TypeSystem;

            var metadataAttrName = typeof(AssemblyMetadataAttribute);
            var metadataAttrType = new TypeReference(metadataAttrName.Namespace, metadataAttrName.Name, mainModule, types.CoreLibrary);
            var metadataAttrCtor = new MethodReference(".ctor", types.Void, metadataAttrType) { HasThis = true };
            metadataAttrCtor.Parameters.Add(new ParameterDefinition(types.String));
            metadataAttrCtor.Parameters.Add(new ParameterDefinition(types.String));

            var metadataAttr = new CustomAttribute(metadataAttrCtor);
            metadataAttr.ConstructorArguments.Add(new CustomAttributeArgument(types.String, k_MarkAsPatchedKey));
            metadataAttr.ConstructorArguments.Add(new CustomAttributeArgument(types.String, k_MarkAsPatchedValue));

            assembly.CustomAttributes.Add(metadataAttr);
        }

        public bool IsPatched(AssemblyDefinition assembly)
        {
            return assembly.CustomAttributes.Any(a =>
                a.AttributeType.FullName == typeof(AssemblyMetadataAttribute).FullName &&
                a.ConstructorArguments.Count == 2 &&
                a.ConstructorArguments[0].Value as string == k_MarkAsPatchedKey &&
                a.ConstructorArguments[1].Value as string == k_MarkAsPatchedValue);
        }

        public bool IsPatched(string assemblyPath)
        {
            using (var assembly = AssemblyDefinition.ReadAssembly(assemblyPath))
                return IsPatched(assembly);
        }

        void Patch(TypeDefinition type)
        {
            if (type.IsInterface)
                return;
            if (type.IsNestedPrivate)
                return;
            if (type.Name == "<Module>")
                return;
            if (type.BaseType.FullName == "System.MulticastDelegate")
                return;
            if (type.IsExplicitLayout)
                return;
            if (type.CustomAttributes.Any(a => a.AttributeType.FullName == typeof(CompilerGeneratedAttribute).FullName))
                return;

            try
            {
                foreach (var method in type.Methods)
                    Patch(method);

                void AddField(string fieldName, FieldAttributes fieldAttributes)
                {
                    type.Fields.Add(new FieldDefinition(fieldName,
                            FieldAttributes.Private | fieldAttributes,
                            type.Module.TypeSystem.Object));
                }

                AddField(MockConstants.InjectedMockStaticDataName, FieldAttributes.Static);
                AddField(MockConstants.InjectedMockDataName, 0);

                AddMockCtor(type);
            }
            catch (Exception e)
            {
                throw new Exception($"Internal error during mock injection into type {type.FullName}", e);
            }
        }

        public static bool IsPatched(TypeDefinition type)
        {
            var mockStaticField = type.Fields.SingleOrDefault(f => f.Name == MockConstants.InjectedMockStaticDataName);
            var mockField = type.Fields.SingleOrDefault(f => f.Name == MockConstants.InjectedMockDataName);
            if ((mockStaticField != null) != (mockField != null))
                throw new Exception("Unexpected mismatch between static and instance mock injected fields");

            return mockStaticField != null;
        }

        void AddMockCtor(TypeDefinition type)
        {
            var ctor = new MethodDefinition(".ctor",
                    MethodAttributes.Public | MethodAttributes.RTSpecialName | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                    type.Module.TypeSystem.Void)
            {
                IsManaged = true,
                DeclaringType = type,
                HasThis = true,
            };
            ctor.Parameters.Add(new ParameterDefinition(type.Module.ImportReference(k_MockPlaceholderType)));

            var il = ctor.Body.GetILProcessor();

            var baseCtors = type.BaseType.Resolve().GetConstructors().Where(c=> !c.IsStatic);

            var baseMockCtor = (MethodReference)baseCtors.SingleOrDefault(c => c.Parameters.SequenceEqual(ctor.Parameters));
            if (baseMockCtor != null)
            {
                baseMockCtor = type.BaseType.IsGenericInstance
                    ? new MethodReference(baseMockCtor.Name, baseMockCtor.ReturnType, type.BaseType) { HasThis = baseMockCtor.HasThis }
                    : type.Module.ImportReference(baseMockCtor);

                il.Append(il.Create(OpCodes.Ldarg_0));
                il.Append(il.Create(OpCodes.Ldarg_1));
                il.Append(il.Create(OpCodes.Call, baseMockCtor));
            }
            else
            {
                var baseCtor = type.Module.ImportReference(baseCtors.Single(c => !c.Parameters.Any()));

                il.Append(il.Create(OpCodes.Ldarg_0));
                il.Append(il.Create(OpCodes.Call, baseCtor));
            }

            il.Append(il.Create(OpCodes.Ret));

            type.Methods.Add(ctor);
        }

        void Patch(MethodDefinition method)
        {
            if (method.IsCompilerControlled || method.IsConstructor || method.IsAbstract || !method.HasBody)
                return;

            var module = method.Module;
            
            method.Body.InitLocals = true;
            var originalType = module.ImportReference(Type.GetType("System.Type"));
            var getTypeFromHandle = module.ImportReference(originalType.Resolve().Methods.Single(m => m.Name == "GetTypeFromHandle"));
            //var getTypeFromHandle = module.Import(new MethodReference("GetTypeFromHandle", type, type) { Parameters = { new ParameterDefinition(runtimeTypeHandle) } });
            var emptyTypes = module.ImportReference(originalType.Resolve().Fields.Single(f => f.Name == "EmptyTypes"));

            var v1 = new VariableDefinition(module.TypeSystem.Object);
            method.Body.Variables.Add(v1);
            var bodyInstructions = new List<Instruction>(method.Body.Instructions);
            method.Body.Instructions.Clear();
            method.Body.Instructions.Add(Instruction.Create(OpCodes.Ldtoken, method.DeclaringType));
            method.Body.Instructions.Add(Instruction.Create(OpCodes.Call, getTypeFromHandle));
            method.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
            method.Body.Instructions.Add(Instruction.Create(OpCodes.Ldtoken, method.ReturnType));
            method.Body.Instructions.Add(Instruction.Create(OpCodes.Call, getTypeFromHandle));
            method.Body.Instructions.Add(Instruction.Create(OpCodes.Ldloca_S, v1));
            method.Body.Instructions.Add(Instruction.Create(OpCodes.Ldsfld, emptyTypes));

            // TODO: Parameter specific
            if (method.Parameters.Count > 0)
            {
                method.Body.Instructions.Add(Instruction.Create(OpCodes.Ldc_I4_1));
                method.Body.Instructions.Add(Instruction.Create(OpCodes.Newarr, module.TypeSystem.Object));
                method.Body.Instructions.Add(Instruction.Create(OpCodes.Dup));
                method.Body.Instructions.Add(Instruction.Create(OpCodes.Ldc_I4_0));
                method.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_1)); // arg
                method.Body.Instructions.Add(Instruction.Create(OpCodes.Box, method.Parameters[0].ParameterType));
                method.Body.Instructions.Add(Instruction.Create(OpCodes.Stelem_Ref));
            }
            else
            {
                method.Body.Instructions.Add(Instruction.Create(OpCodes.Ldc_I4_0));
                method.Body.Instructions.Add(Instruction.Create(OpCodes.Newarr, module.TypeSystem.Object));
            }

            // End of parameter include
            method.Body.Instructions.Add(Instruction.Create(OpCodes.Call, module.ImportReference(k_PatchedAssemblyBridgeTryMock)));
            method.Body.Instructions.Add(Instruction.Create(OpCodes.Nop));

            var count = method.Body.Instructions.Count;

            var hasReturnValue = method.ReturnType != module.TypeSystem.Void;
            if (hasReturnValue)
            {
                method.Body.Instructions.Add(Instruction.Create(OpCodes.Ldloc_S, v1));
                method.Body.Instructions.Add(Instruction.Create(OpCodes.Unbox_Any, method.ReturnType));
            }
            method.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));

            foreach (var instruction in bodyInstructions)
            {
                method.Body.Instructions.Add(instruction);
            }
            method.Body.Instructions[count - 1] = Instruction.Create(OpCodes.Brfalse_S, method.Body.Instructions[count + (hasReturnValue ? 3 : 1)]);

            /*method.Body.Instructions.Clear();


            ConvertReturnTypeToDefault(method.ReturnType, method.Body.Instructions);
            method.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));*/
        }
    }
}
