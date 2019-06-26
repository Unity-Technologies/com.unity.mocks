using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NSubstitute.Core;
using NSubstitute.Elevated.Internals;
using NSubstitute.Exceptions;
using NSubstitute.Proxies;
using NSubstitute.Proxies.CastleDynamicProxy;
using NSubstitute.Proxies.DelegateProxy;
using Unity.Utils;

namespace NSubstitute.Elevated
{
    class ElevatedSubstituteManager : IProxyFactory, IDisposable
    {
        readonly CallFactory m_CallFactory;
        readonly IProxyFactory m_DefaultProxyFactory = new ProxyFactory(new DelegateProxyFactory(), new CastleDynamicProxyFactory());
        readonly object[] k_MockedCtorParams = { new MockPlaceholderType() };
        Dictionary<object, ICallRouter> m_Routers = new Dictionary<object, ICallRouter>();

        public ElevatedSubstituteManager(ISubstitutionContext substitutionContext)
        {
            m_CallFactory = new CallFactory(substitutionContext);
        }

        public void Dispose()
        {
            var leaks = m_Routers.Keys.OfType<Type>().StringJoin(", ");
            if (!leaks.IsEmpty())
                throw new Exception("Test forgot to dispose SubstituteStatic.For<T>() where T is " + leaks);
        }
        
        object IProxyFactory.GenerateProxy(ICallRouter callRouter, Type typeToProxy, Type[] additionalInterfaces, object[] constructorArguments)
        {
            // TODO:
            //  * new type MockCtorPlaceholder in elevated assy
            //  * generate new empty ctor that takes MockCtorPlaceholder in all mocked types
            //  * support ctor params. throw if foudn and not ForPartsOf. then ForPartsOf determines which ctor we use.
            //  * have a note about static ctors. because they are special, and do not support disposal, can't really mock them right.
            //    best for user to do mock/unmock of static ctors manually (i.e. move into StaticInit/StaticDispose and call directly from test code)

            object proxy;
            var substituteConfig = ElevatedSubstitutionContext.TryGetSubstituteConfig(callRouter);

            if (typeToProxy.IsInterface || substituteConfig == null)
            {
                proxy = m_DefaultProxyFactory.GenerateProxy(callRouter, typeToProxy, additionalInterfaces, constructorArguments);
            }
            else if (typeToProxy == typeof(SubstituteStatic.Proxy))
            {
                if (additionalInterfaces?.Any() == true)
                    throw new SubstituteException("Cannot substitute interfaces as static");
                if (constructorArguments.Length != 1)
                    throw new SubstituteException("Unexpected use of SubstituteStatic.For");

                // the type we want comes from SubstituteStatic.For as a single ctor arg
                var actualType = (Type)constructorArguments[0];

                proxy = CreateStaticProxy(actualType, callRouter);
            }
            else
            {
                // requests for additional interfaces on patched types cannot be done at runtime. elevated mocking can't,
                // by definition, go through a runtime dynamic proxy generator that could add such things.
                if (additionalInterfaces.Any())
                    throw new SubstituteException("Cannot add interfaces at runtime to patched types");

                switch (substituteConfig)
                {
                    // TODO: i misunderstood "override all" and "call base", so fix these
                    // need to store the yes/no flag with the router in the value of the dict and use that when routing for return to TryMock
                    
                    case SubstituteConfig.OverrideAllCalls:

                        // overriding all calls includes the ctor, so it makes no sense for the user to pass in ctor args
                        if (constructorArguments != null && constructorArguments.Any())
                            throw new SubstituteException("Do not pass ctor args when substituting with elevated mocks (or did you mean to use ForPartsOf?)");

                        // but we use a ctor arg to select the special empty ctor that we patched in
                        //$$$ TODO constructorArguments = k_MockedCtorParams;
                        break;
                    
                    case SubstituteConfig.CallBaseByDefault:
                        // TODO: this is just wrong
                        var castleDynamicProxyFactory = new CastleDynamicProxyFactory();
                        return castleDynamicProxyFactory.GenerateProxy(callRouter, typeToProxy, additionalInterfaces, constructorArguments);
                    
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                proxy = Activator.CreateInstance(typeToProxy, constructorArguments);
                m_Routers.Add(proxy, callRouter);
            }

            return proxy;
        }
        
        object CreateStaticProxy(Type typeToProxy, ICallRouter callRouter)
        {
            if (!m_Routers.TryAdd(typeToProxy, callRouter))
                throw new SubstituteException("Cannot substitute the same type twice (did you forget to Dispose() your previous substitute?)");

            return new SubstituteStatic.Proxy(new DelegateDisposable(() =>
                {
                    if (!m_Routers.TryGetValue(typeToProxy, out var found))
                        throw new SubstituteException("Unexpected static unmock of an already-unmocked type");

                    if (found != callRouter)
                        throw new SubstituteException("Discovered unexpected call router attached in static mock context");

                    m_Routers.Remove(typeToProxy);
                }));
        }

        // called from patched assembly code via the PatchedAssemblyBridge. return true if the mock is handling the behavior.
        // false means that the original implementation should run.
        public bool TryMock(Type actualType, object instance, Type mockedReturnType, out object mockedReturnValue, MethodInfo method, object[] args)
        {
            if (m_Routers.TryGetValue(instance ?? actualType, out var callRouter))
            {
                var shouldCallOriginalMethod = false;
                var call = m_CallFactory.Create(method, args, instance, () => shouldCallOriginalMethod = true);
                mockedReturnValue = callRouter.Route(call);

                return !shouldCallOriginalMethod;
            }

            mockedReturnValue = mockedReturnType.GetDefaultValue();
            return false;
        }
    }
}
