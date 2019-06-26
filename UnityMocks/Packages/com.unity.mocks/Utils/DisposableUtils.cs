using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Unity.Utils
{
    public class DelegateDisposable : IDisposable
    {
        readonly Action m_DisposeAction;

        public DelegateDisposable([NotNull] Action disposeAction) => m_DisposeAction = disposeAction;
        public void Dispose() => m_DisposeAction();
    }

    public class DisposableList<T> : List<T>, IDisposable
        where T : IDisposable
    {
        public DisposableList() { }

        public DisposableList([NotNull] IEnumerable<T> collection)
            : base(collection) { }

        public DisposableList(int capacity)
            : base(capacity) { }

        public void Dispose()
        {
            foreach (var item in this)
                item.Dispose();
        }
    }

    public static partial class ListExtensions
    {
        public static DisposableList<T> ToDisposableList<T>(this IEnumerable<T> @this)
            where T : IDisposable
            => new DisposableList<T>(@this);
    }
}
