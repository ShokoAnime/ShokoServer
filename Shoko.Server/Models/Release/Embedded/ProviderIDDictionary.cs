using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Shoko.Server.Utilities;

namespace Shoko.Server.Models.Release;

/// <summary>
///   The provider IDs of a release's cross-reference: a small dictionary kept
///   as one array in insertion order, with its keys and values pooled.
/// </summary>
/// <remarks>
///   Holds two or three entries where a <see cref="Dictionary{TKey,TValue}"/>
///   would need buckets and slack, and enumerates in the order the entries were
///   added, as a dictionary does until an entry is removed.
/// </remarks>
public sealed class ProviderIDDictionary : IDictionary<string, string>, IReadOnlyDictionary<string, string>
{
    #region Fields

    private KeyValuePair<string, string>[] _entries = [];

    private int _count;

    #endregion

    #region Constructors

    /// <summary>
    ///   An empty dictionary.
    /// </summary>
    public ProviderIDDictionary() { }

    /// <summary>
    ///   A copy of some provider IDs.
    /// </summary>
    /// <param name="entries">The provider IDs to copy.</param>
    public ProviderIDDictionary(IEnumerable<KeyValuePair<string, string>> entries)
    {
        foreach (var (key, value) in entries)
            Add(key, value);
    }

    #endregion

    #region Properties

    /// <inheritdoc/>
    public int Count => _count;

    /// <inheritdoc/>
    public bool IsReadOnly => false;

    /// <inheritdoc/>
    public ICollection<string> Keys => Enumerate().Select(entry => entry.Key).ToList();

    /// <inheritdoc/>
    public ICollection<string> Values => Enumerate().Select(entry => entry.Value).ToList();

    IEnumerable<string> IReadOnlyDictionary<string, string>.Keys => Keys;

    IEnumerable<string> IReadOnlyDictionary<string, string>.Values => Values;

    /// <inheritdoc/>
    public string this[string key]
    {
        get => TryGetValue(key, out var value) ? value : throw new KeyNotFoundException($"The given key '{key}' was not present in the dictionary.");
        set
        {
            if (IndexOf(key) is var index and >= 0)
                _entries[index] = new(_entries[index].Key, StringPool.Get(value));
            else
                Append(key, value);
        }
    }

    #endregion

    #region Methods

    /// <inheritdoc/>
    public void Add(string key, string value)
    {
        if (IndexOf(key) >= 0)
            throw new ArgumentException($"An item with the same key has already been added. Key: {key}", nameof(key));

        Append(key, value);
    }

    /// <inheritdoc/>
    public void Add(KeyValuePair<string, string> item)
        => Add(item.Key, item.Value);

    /// <inheritdoc/>
    public bool ContainsKey(string key)
        => IndexOf(key) >= 0;

    /// <inheritdoc/>
    public bool Contains(KeyValuePair<string, string> item)
        => IndexOf(item.Key) is var index and >= 0 && string.Equals(_entries[index].Value, item.Value);

    /// <inheritdoc/>
    public bool TryGetValue(string key, [MaybeNullWhen(false)] out string value)
    {
        if (IndexOf(key) is var index and >= 0)
        {
            value = _entries[index].Value;
            return true;
        }

        value = null;
        return false;
    }

    /// <inheritdoc/>
    public bool Remove(string key)
    {
        var index = IndexOf(key);
        if (index < 0)
            return false;

        Array.Copy(_entries, index + 1, _entries, index, _count - index - 1);
        _entries[--_count] = default;
        return true;
    }

    /// <inheritdoc/>
    public bool Remove(KeyValuePair<string, string> item)
        => Contains(item) && Remove(item.Key);

    /// <inheritdoc/>
    public void Clear()
    {
        _entries = [];
        _count = 0;
    }

    /// <inheritdoc/>
    public void CopyTo(KeyValuePair<string, string>[] array, int arrayIndex)
        => Array.Copy(_entries, 0, array, arrayIndex, _count);

    /// <inheritdoc/>
    public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        => Enumerate().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    /// <summary>
    ///   Finds the position of a key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The position, or -1 when the key is missing.</returns>
    /// <exception cref="ArgumentNullException">The key is <c>null</c>.</exception>
    private int IndexOf(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        for (var index = 0; index < _count; index++)
        {
            if (string.Equals(_entries[index].Key, key))
                return index;
        }

        return -1;
    }

    /// <summary>
    ///   Adds an entry at the end, growing the array by as little as fits.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    private void Append(string key, string value)
    {
        if (_count == _entries.Length)
            Array.Resize(ref _entries, _count is 0 ? 2 : _count * 2);

        _entries[_count++] = new(StringPool.Get(key), StringPool.Get(value));
    }

    /// <summary>
    ///   Lists the entries in the order they were added.
    /// </summary>
    /// <returns>The entries.</returns>
    private IEnumerable<KeyValuePair<string, string>> Enumerate()
        => new ArraySegment<KeyValuePair<string, string>>(_entries, 0, _count);

    #endregion
}
