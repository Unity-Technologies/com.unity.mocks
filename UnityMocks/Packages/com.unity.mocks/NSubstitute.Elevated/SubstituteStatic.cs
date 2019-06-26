using System;
using JetBrains.Annotations;

namespace NSubstitute
{
    [PublicAPI]
    public static class SubstituteStatic
    {
        // callers need an actual object in order to chain further arranging, so we return this placeholder for static substitutes
        // TODO: add a DisposeSentinel
        public class Proxy : IDisposable
        {
            readonly IDisposable m_Forwarder;

            internal Proxy(IDisposable forwarder) => m_Forwarder = forwarder;
            public void Dispose() => m_Forwarder.Dispose();
        }

        // best to wrap static substitutes in `using` so they will auto-dispose. this is important because we're dealing with
        // statics, which are global, and without cleanup, substitute will accidentally leak across tests.
        public static Proxy For<T>() => For(typeof(T));
        public static Proxy For(Type staticType) => Substitute.For<Proxy>(staticType);
    }
}
