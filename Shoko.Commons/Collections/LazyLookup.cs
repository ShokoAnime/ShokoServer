using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Shoko.Commons.Collections
{
    public class LazyLookup<TKey, TElement> : ILookup<TKey, TElement>
    {
        private readonly Lazy<ILookup<TKey, TElement>> _lookup;

        public LazyLookup(Lazy<ILookup<TKey, TElement>> lookup)
        {
            if (lookup == null)
                throw new ArgumentNullException(nameof(lookup));

            _lookup = lookup;
        }

        public static LazyLookup<TKey, TElement> Create(IEnumerable<TElement> source, Func<TElement, TKey> keySelector,
            IEqualityComparer<TKey> comparer = null)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (keySelector == null)
                throw new ArgumentNullException(nameof(keySelector));

            var lazyLookup = new Lazy<ILookup<TKey, TElement>>(() => source.ToLookup(keySelector, comparer), false);

            return new LazyLookup<TKey, TElement>(lazyLookup);
        }

        public static LazyLookup<TKey, TElement> Create<TSource>(IEnumerable<TSource> source, Func<TSource, TKey> keySelector,
            Func<TSource, TElement> elementSelector, IEqualityComparer<TKey> comparer = null)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (keySelector == null)
                throw new ArgumentNullException(nameof(keySelector));
            if (elementSelector == null)
                throw new ArgumentNullException(nameof(elementSelector));

            var lazyLookup = new Lazy<ILookup<TKey, TElement>>(() => source.ToLookup(keySelector, elementSelector, comparer), false);

            return new LazyLookup<TKey, TElement>(lazyLookup);
        }

        public bool Contains(TKey key) => _lookup.Value.Contains(key);

        public IEnumerator<IGrouping<TKey, TElement>> GetEnumerator() => _lookup.Value.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _lookup.Value.GetEnumerator();

        public IEnumerable<TElement> this[TKey key] => _lookup.Value[key];

        public int Count => _lookup.Value.Count;
    }
}