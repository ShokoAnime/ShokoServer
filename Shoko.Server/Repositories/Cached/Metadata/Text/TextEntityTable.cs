using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;

namespace Shoko.Server.Repositories.Cached.Metadata.Text;

/// <summary>
///   An entry as the text cache keys it: its source and kind as indexes in the
///   cache's own tables, packed with a numeric ID, or with the ID kept as text
///   when it is not a number.
/// </summary>
/// <param name="Packed">
///   The source index in the top byte, the kind index in the next one, and a
///   numeric ID in the low 48 bits, which are <c>0</c> for a text ID.
/// </param>
/// <param name="Name">The ID when it is not a canonical number that fits in 48 bits.</param>
internal readonly record struct TextEntityKey(long Packed, string? Name)
{
    /// <summary>
    ///   The largest numeric ID packed into the key.
    /// </summary>
    internal const long MaxNumber = (1L << 48) - 1;

    /// <summary>
    ///   The index of the entry's source.
    /// </summary>
    internal byte Source => (byte)((ulong)Packed >> 56);

    /// <summary>
    ///   The index of the entry's kind.
    /// </summary>
    internal byte EntityType => (byte)((ulong)Packed >> 48);

    /// <summary>
    ///   The entry's numeric ID, for a key without a <see cref="Name"/>.
    /// </summary>
    internal long Number => Packed & MaxNumber;

    /// <summary>
    ///   Packs a source index, a kind index and an ID into a key.
    /// </summary>
    /// <param name="source">The source index.</param>
    /// <param name="entityType">The kind index, never <c>0</c>.</param>
    /// <param name="id">The entry's own ID.</param>
    /// <returns>The key.</returns>
    internal static TextEntityKey Create(byte source, byte entityType, string id)
    {
        var prefix = ((long)source << 56) | ((long)entityType << 48);
        return IsPackable(id, out var number) ? new(prefix | number, null) : new(prefix, id);
    }

    /// <summary>
    ///   Whether an ID is written as a canonical number small enough to pack.
    /// </summary>
    /// <param name="id">The ID.</param>
    /// <param name="number">The number, when it is one.</param>
    /// <returns><c>true</c> when the ID can be packed.</returns>
    private static bool IsPackable(string id, out long number)
    {
        number = 0;
        if (id.Length is 0 or > 15 || (id.Length > 1 && id[0] is '0'))
            return false;

        foreach (var c in id)
        {
            if (c is < '0' or > '9')
                return false;

            number = number * 10 + (c - '0');
        }

        return number <= MaxNumber;
    }
}

/// <summary>
///   Maps every entry to the array of its stored texts. Reads never lock:
///   numeric keys live in an open-addressing table whose slots are published
///   value first, then key, and text keys in a concurrent dictionary.
/// </summary>
/// <remarks>
///   Writes must be made under one lock held by the caller. An entry that
///   loses every text keeps its slot with an empty array until the next resize.
/// </remarks>
internal sealed class TextEntityTable
{
    /// <summary>
    ///   One generation of the numeric table.
    /// </summary>
    /// <param name="size">The number of slots, a power of two.</param>
    private sealed class Buckets(int size)
    {
        internal readonly long[] Keys = new long[size];

        internal readonly TextRow[]?[] Values = new TextRow[]?[size];

        internal readonly int Shift = 64 - BitOperations.Log2((uint)size);

        internal int Count;

        internal int Mask => Keys.Length - 1;

        internal int Start(long key)
            => (int)(((ulong)key * 0x9E3779B97F4A7C15UL) >> Shift);
    }

    private Buckets _buckets = new(1024);

    private readonly ConcurrentDictionary<TextEntityKey, TextRow[]> _named = new();

    /// <summary>
    ///   How many entries have a slot, with or without texts.
    /// </summary>
    internal int Count => Volatile.Read(ref _buckets).Count + _named.Count;

    /// <summary>
    ///   The texts of an entry.
    /// </summary>
    /// <param name="key">The entry.</param>
    /// <returns>The entry's rows, or <c>null</c> when it has none.</returns>
    internal TextRow[]? Get(TextEntityKey key)
    {
        if (key.Name is not null)
            return _named.GetValueOrDefault(key);

        var buckets = Volatile.Read(ref _buckets);
        var mask = buckets.Mask;
        for (var index = buckets.Start(key.Packed); ; index = (index + 1) & mask)
        {
            var stored = Volatile.Read(ref buckets.Keys[index]);
            if (stored == key.Packed)
                return Volatile.Read(ref buckets.Values[index]);
            if (stored is 0)
                return null;
        }
    }

    /// <summary>
    ///   Replaces the texts of an entry. Called under the caller's write lock.
    /// </summary>
    /// <param name="key">The entry.</param>
    /// <param name="rows">The entry's new rows; empty or <c>null</c> when it has none left.</param>
    internal void Set(TextEntityKey key, TextRow[]? rows)
    {
        if (key.Name is not null)
        {
            if (rows is null or { Length: 0 })
                _named.TryRemove(key, out _);
            else
                _named[key] = rows;
            return;
        }

        var buckets = _buckets;
        if (Find(buckets, key.Packed, out var index))
        {
            Volatile.Write(ref buckets.Values[index], rows);
            return;
        }

        if (rows is null or { Length: 0 })
            return;

        if ((buckets.Count + 1) * 10 > buckets.Keys.Length * 7)
        {
            buckets = Grow(buckets);
            Find(buckets, key.Packed, out index);
        }

        // The value goes in before the key, so a reader that sees the key
        // also sees its rows.
        Volatile.Write(ref buckets.Values[index], rows);
        Volatile.Write(ref buckets.Keys[index], key.Packed);
        buckets.Count++;
    }

    /// <summary>
    ///   Every entry with texts, and its rows.
    /// </summary>
    /// <returns>The entries, in no particular order.</returns>
    internal IEnumerable<(TextEntityKey Key, TextRow[] Rows)> Enumerate()
    {
        var buckets = Volatile.Read(ref _buckets);
        for (var index = 0; index < buckets.Keys.Length; index++)
        {
            var key = Volatile.Read(ref buckets.Keys[index]);
            if (key is 0 || Volatile.Read(ref buckets.Values[index]) is not { Length: > 0 } rows)
                continue;

            yield return (new(key, null), rows);
        }

        foreach (var (key, rows) in _named)
            yield return (key, rows);
    }

    /// <summary>
    ///   Finds the slot of a key, or the empty slot it would take.
    /// </summary>
    /// <param name="buckets">The table.</param>
    /// <param name="key">The packed key.</param>
    /// <param name="index">The slot.</param>
    /// <returns><c>true</c> when the key is in the table.</returns>
    private static bool Find(Buckets buckets, long key, out int index)
    {
        var mask = buckets.Mask;
        for (index = buckets.Start(key); ; index = (index + 1) & mask)
        {
            var stored = buckets.Keys[index];
            if (stored == key)
                return true;
            if (stored is 0)
                return false;
        }
    }

    /// <summary>
    ///   Moves every entry with texts into a table twice the size, and
    ///   publishes it.
    /// </summary>
    /// <param name="old">The full table.</param>
    /// <returns>The new table.</returns>
    private Buckets Grow(Buckets old)
    {
        var live = 0;
        for (var index = 0; index < old.Keys.Length; index++)
            if (old.Keys[index] is not 0 && old.Values[index] is { Length: > 0 })
                live++;

        var size = (int)BitOperations.RoundUpToPowerOf2((uint)Math.Max(1024, (live + 1) * 2));
        var grown = new Buckets(size);
        for (var index = 0; index < old.Keys.Length; index++)
        {
            var key = old.Keys[index];
            if (key is 0 || old.Values[index] is not { Length: > 0 } rows)
                continue;

            Find(grown, key, out var slot);
            grown.Keys[slot] = key;
            grown.Values[slot] = rows;
            grown.Count++;
        }

        Volatile.Write(ref _buckets, grown);
        return grown;
    }
}
