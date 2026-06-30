using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Shoko.Server.Filters;

public sealed class LazyDictionary<TKey, TValue>(Dictionary<TKey, Lazy<TValue>>? dictionary = null) : IReadOnlyDictionary<TKey, TValue> where TKey : notnull
{
    private readonly Dictionary<TKey, Lazy<TValue>> _dictionary = dictionary ?? [];

    public TValue this[TKey key] => _dictionary[key].Value;

    public IEnumerable<TKey> Keys => _dictionary.Keys;

    public IEnumerable<TValue> Values => _dictionary.Values.Select(a => a.Value);

    public int Count => _dictionary.Count;

    public bool ContainsKey(TKey key)
        => _dictionary.ContainsKey(key);

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        => _dictionary.Select(a => new KeyValuePair<TKey, TValue>(a.Key, a.Value.Value)).GetEnumerator();

    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        if (_dictionary.TryGetValue(key, out var lazy))
        {
            value = lazy.Value;
            return true;
        }

        value = default;
        return false;
    }

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();
}
