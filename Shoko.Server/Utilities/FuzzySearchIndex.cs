using System;
using System.Collections.Generic;
using System.Linq;

namespace Shoko.Server.Utilities;

public class FuzzySearchIndex<T>
{
    private List<(T Item, List<(string Original, string Normalized)> Titles)> _items = [];
    private Dictionary<string, List<int>> _latinIndex = new();

    public void Build(IEnumerable<T> items, Func<T, IEnumerable<string>> titleExtractor)
    {
        _items = [];
        _latinIndex = new Dictionary<string, List<int>>();

        foreach (var item in items)
        {
            var idx = _items.Count;
            var titles = new List<(string Original, string Normalized)>();
            var seenTrigrams = new HashSet<string>();

            foreach (var raw in titleExtractor(item) ?? [])
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
                    bucket.Add(idx);
                }
            }

            _items.Add((item, titles));
        }
    }

    public IEnumerable<SeriesSearch.SearchResult<T>> Search(string query, bool fuzzy = true, int? limit = null)
    {
        if (string.IsNullOrWhiteSpace(query) || _items.Count == 0)
            return [];

        var normalizedQuery = SeriesSearch.NormalizeForIndex(query);
        if (string.IsNullOrWhiteSpace(normalizedQuery))
            return [];

        var results = SeriesSearch.IsLatinScript(normalizedQuery)
            ? SearchLatin(normalizedQuery, fuzzy)
            : SearchContains(normalizedQuery);

        results = results.OrderBy(r => r);
        return limit.HasValue ? results.Take(limit.Value) : results;
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
        foreach (var (item, titles) in _items)
        {
            SeriesSearch.SearchResult<T> best = null;
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
            SeriesSearch.SearchResult<T> best = null;

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
                else if (fuzzy && normalized.Length >= normalizedQuery.Length - maxErrors && SeriesSearch.IsLatinScript(normalized))
                {
                    var dist = SeriesSearch.MinEditDistInText(normalizedQuery, normalized);
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
