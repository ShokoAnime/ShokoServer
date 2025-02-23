using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Shoko.Commons.Collections
{
    public class EmptyLookup<TKey, TElement> : ILookup<TKey, TElement>
    {
        public static readonly EmptyLookup<TKey, TElement> Instance = new EmptyLookup<TKey, TElement>();

        public bool Contains(TKey key) => false;

        public IEnumerator<IGrouping<TKey, TElement>> GetEnumerator() => Enumerable.Empty<IGrouping<TKey, TElement>>().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => Enumerable.Empty<IGrouping<TKey, TElement>>().GetEnumerator();

        public IEnumerable<TElement> this[TKey key] => Enumerable.Empty<TElement>();

        public int Count => 0;
    }
}