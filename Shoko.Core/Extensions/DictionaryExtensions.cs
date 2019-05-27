using System.Collections.Generic;

namespace Shoko.Core.Extensions
{
    public static class DictionaryExtensions
    {
        public static void Deconstruct<K, V>(this KeyValuePair<K, V> kv, out K key, out V value)
        {
            key = kv.Key;
            value = kv.Value;
        }
    }
}