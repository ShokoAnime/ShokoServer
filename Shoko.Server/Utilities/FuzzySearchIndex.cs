using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Shoko.Server.Utilities;

public class FuzzySearchIndex<T>
{
    private readonly List<(T Item, List<(string Original, string Normalized)> Titles)> _items = [];
    private readonly List<HashSet<string>> _trigrams = [];
    private readonly List<bool> _live = [];
    private readonly Dictionary<string, List<int>> _latinIndex = new();
    private readonly Dictionary<object, int> _slots = [];
    private readonly ReaderWriterLockSlim _lock = new();
    private Func<T, object> _keyOf = item => item!;

    /// <summary>
    ///   How many slots were left behind by updated or removed items.
    /// </summary>
    public int Waste
    {
        get
        {
            _lock.EnterReadLock();
            try
            {
                return _items.Count - _slots.Count;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }

    /// <summary>
    ///   How many items are in the index.
    /// </summary>
    public int Count
    {
        get
        {
            _lock.EnterReadLock();
            try
            {
                return _slots.Count;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }

    public void Build(IEnumerable<T> items, Func<T, IEnumerable<string>> titleExtractor)
        => Build(items, titleExtractor, item => item!);

    /// <summary>
    ///   Fills the index, replacing anything in it.
    /// </summary>
    /// <param name="items">The items.</param>
    /// <param name="titleExtractor">The titles an item is found by.</param>
    /// <param name="keyOf">What an item is known by when it is updated or removed later.</param>
    public void Build(IEnumerable<T> items, Func<T, IEnumerable<string>> titleExtractor, Func<T, object> keyOf)
    {
        _lock.EnterWriteLock();
        try
        {
            _keyOf = keyOf;
            _items.Clear();
            _trigrams.Clear();
            _live.Clear();
            _latinIndex.Clear();
            _slots.Clear();
            foreach (var item in items)
                Add(item, titleExtractor(item));
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    ///   Puts an item in the index with the titles it has now, replacing the
    ///   titles it had. An item already in the index keeps its place, so
    ///   results that rank the same come back in the same order.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="titles">The titles it is found by.</param>
    public void Upsert(T item, IEnumerable<string> titles)
    {
        var list = titles.ToList();
        _lock.EnterWriteLock();
        try
        {
            if (_slots.TryGetValue(_keyOf(item), out var slot))
            {
                ClearSlot(slot);
                Fill(slot, item, list);
                _live[slot] = true;
            }
            else
            {
                Add(item, list);
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    ///   Takes an item out of the index.
    /// </summary>
    /// <param name="key">What the item is known by.</param>
    public void Remove(object key)
    {
        _lock.EnterWriteLock();
        try
        {
            RemoveUnsafe(key);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    private void RemoveUnsafe(object key)
    {
        if (!_slots.Remove(key, out var slot))
            return;

        ClearSlot(slot);
        _live[slot] = false;
    }

    /// <summary>
    ///   Empties a slot and takes it out of the trigram buckets.
    /// </summary>
    /// <param name="slot">The slot.</param>
    private void ClearSlot(int slot)
    {
        foreach (var trigram in _trigrams[slot])
            if (_latinIndex.TryGetValue(trigram, out var bucket))
            {
                var at = bucket.BinarySearch(slot);
                if (at >= 0)
                    bucket.RemoveAt(at);
                if (bucket.Count is 0)
                    _latinIndex.Remove(trigram);
            }

        _items[slot] = (default!, []);
        _trigrams[slot] = [];
    }

    private void Add(T item, IEnumerable<string> rawTitles)
    {
        var idx = _items.Count;
        _items.Add((default!, []));
        _trigrams.Add([]);
        _live.Add(true);
        Fill(idx, item, rawTitles);
    }

    /// <summary>
    ///   Puts an item and its titles in a slot, keeping every trigram bucket
    ///   in slot order, as a fresh build lays them out.
    /// </summary>
    /// <param name="idx">The slot.</param>
    /// <param name="item">The item.</param>
    /// <param name="rawTitles">The titles it is found by.</param>
    private void Fill(int idx, T item, IEnumerable<string> rawTitles)
    {
        var titles = new List<(string Original, string Normalized)>();
        var seenTrigrams = new HashSet<string>();

        foreach (var raw in rawTitles ?? [])
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            var normalized = SeriesSearch.NormalizeForIndex(raw);
            if (string.IsNullOrWhiteSpace(normalized))
                continue;

            titles.Add((raw, normalized));

            if (!SeriesSearch.IsLatinScript(normalized))
                continue;

            foreach (var trigram in ComputeTrigrams(normalized))
            {
                if (!seenTrigrams.Add(trigram))
                    continue;

                if (!_latinIndex.TryGetValue(trigram, out var bucket))
                    _latinIndex[trigram] = bucket = [];
                if (bucket.Count is 0 || bucket[^1] < idx)
                    bucket.Add(idx);
                else
                    bucket.Insert(~bucket.BinarySearch(idx), idx);
            }
        }

        _items[idx] = (item, titles);
        _trigrams[idx] = seenTrigrams;
        _slots[_keyOf(item)] = idx;
    }

    public IEnumerable<SeriesSearch.SearchResult<T>> Search(string query, bool fuzzy = true, int? limit = null)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        var normalizedQuery = SeriesSearch.NormalizeForIndex(query);
        if (string.IsNullOrWhiteSpace(normalizedQuery))
            return [];

        List<SeriesSearch.SearchResult<T>> results;
        _lock.EnterReadLock();
        try
        {
            if (_slots.Count == 0)
                return [];

            results = SeriesSearch.IsLatinScript(normalizedQuery)
                ? [.. SearchLatin(normalizedQuery, fuzzy)]
                : [.. SearchContains(normalizedQuery)];
        }
        finally
        {
            _lock.ExitReadLock();
        }

        var ordered = results.OrderBy(r => r);
        return limit.HasValue ? [.. ordered.Take(limit.Value)] : [.. ordered];
    }

    private static IEnumerable<string> ComputeTrigrams(string normalized)
    {
        if (normalized.Length < 3)
        {
            yield return normalized;
            yield break;
        }

        for (var i = 0; i <= normalized.Length - 3; i++)
            yield return normalized.Substring(i, 3);
    }

    private IEnumerable<SeriesSearch.SearchResult<T>> SearchContains(string normalizedQuery)
    {
        for (var slot = 0; slot < _items.Count; slot++)
        {
            if (!_live[slot])
                continue;

            var (item, titles) = _items[slot];
            SeriesSearch.SearchResult<T>? best = null;
            foreach (var (original, normalized) in titles)
            {
                var idx = normalized.IndexOf(normalizedQuery, StringComparison.Ordinal);
                if (idx < 0)
                    continue;

                var result = new SeriesSearch.SearchResult<T>
                {
                    ExactMatch = true,
                    Index = idx,
                    Distance = 0,
                    LengthDifference = Math.Abs(normalizedQuery.Length - normalized.Length),
                    Match = original,
                    Result = item,
                };

                if (result.CompareTo(best) < 0)
                    best = result;
            }

            if (best != null)
                yield return best;
        }
    }

    private IEnumerable<SeriesSearch.SearchResult<T>> SearchLatin(string normalizedQuery, bool fuzzy)
    {
        var maxErrors = fuzzy ? SeriesSearch.GetMaxErrors(normalizedQuery.Length) : 0;
        var queryTrigrams = ComputeTrigrams(normalizedQuery).Distinct().ToList();

        var candidateSet = new HashSet<int>();
        foreach (var trigram in queryTrigrams)
        {
            if (_latinIndex.TryGetValue(trigram, out var indices))
                foreach (var idx in indices)
                    candidateSet.Add(idx);
        }

        foreach (var itemIdx in candidateSet)
        {
            var (item, titles) = _items[itemIdx];
            SeriesSearch.SearchResult<T>? best = null;

            foreach (var (original, normalized) in titles)
            {
                SeriesSearch.SearchResult<T> result;

                var containsIdx = normalized.IndexOf(normalizedQuery, StringComparison.Ordinal);
                if (containsIdx >= 0)
                {
                    result = new SeriesSearch.SearchResult<T>
                    {
                        ExactMatch = true,
                        Index = containsIdx,
                        Distance = 0,
                        LengthDifference = Math.Abs(normalizedQuery.Length - normalized.Length),
                        Match = original,
                        Result = item,
                    };
                }
                else if (fuzzy && SeriesSearch.IsLatinScript(normalized) && SeriesSearch.TryGetFuzzyDistance(normalizedQuery, normalized, out var dist))
                {
                    if (dist > maxErrors)
                        continue;

                    result = new SeriesSearch.SearchResult<T>
                    {
                        ExactMatch = false,
                        Index = 0,
                        Distance = normalizedQuery.Length > 0 ? (double)dist / normalizedQuery.Length : 0,
                        LengthDifference = Math.Abs(normalizedQuery.Length - normalized.Length),
                        Match = original,
                        Result = item,
                    };
                }
                else
                {
                    continue;
                }

                if (result.CompareTo(best) < 0)
                    best = result;
            }

            if (best != null)
                yield return best;
        }
    }
}
