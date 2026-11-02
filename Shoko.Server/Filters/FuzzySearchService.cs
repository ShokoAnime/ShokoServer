using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Shoko.Abstractions.Filtering.Services;
using Shoko.Server.Utilities;

namespace Shoko.Server.Filters;

public class FuzzySearchService : IFuzzySearchService
{
    /// <summary>
    ///   How many distinct queries keep their scores. Each one holds a score
    ///   for every name set it was matched against, a few MiB on a large
    ///   collection, so the least recently used query is dropped first.
    /// </summary>
    internal const int MaxCachedQueries = 16;

    private readonly object _lock = new();

    private readonly LinkedList<(string Query, ConditionalWeakTable<IReadOnlySet<string>, StrongBox<(bool, int, double, int)?>> Scores)> _recent = new();

    private readonly Dictionary<string, LinkedListNode<(string Query, ConditionalWeakTable<IReadOnlySet<string>, StrongBox<(bool, int, double, int)?>> Scores)>> _byQuery = new(StringComparer.Ordinal);

    // The query a filter is evaluating repeats for every series, so the last one is found without the lock.
    private volatile Tuple<string, ConditionalWeakTable<IReadOnlySet<string>, StrongBox<(bool, int, double, int)?>>>? _last;

    /// <summary>
    ///   The number of queries whose scores are kept.
    /// </summary>
    internal int CachedQueryCount
    {
        get
        {
            lock (_lock)
                return _byQuery.Count;
        }
    }

    /// <inheritdoc/>
    public void InvalidateCache()
    {
        lock (_lock)
        {
            _recent.Clear();
            _byQuery.Clear();
            _last = null;
        }
    }

    /// <inheritdoc/>
    public bool FuzzyMatchesAnyName(string query, IReadOnlySet<string> names)
        => FuzzyScoreAnyName(query, names) is not null;

    /// <inheritdoc/>
    public (bool isNotExact, int index, double distance, int lengthDiff)? FuzzyScoreAnyName(string query, IReadOnlySet<string> names)
    {
        var scores = GetScores(query);
        if (scores.TryGetValue(names, out var cached))
            return cached.Value;

        var score = ComputeFuzzyScore(query, names);
        scores.AddOrUpdate(names, new(score));
        return score;
    }

    /// <summary>
    ///   Gets the scores kept for a query, making it the most recently used
    ///   and dropping the least recently used beyond <see cref="MaxCachedQueries"/>.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <returns>The query's scores, by name set.</returns>
    private ConditionalWeakTable<IReadOnlySet<string>, StrongBox<(bool, int, double, int)?>> GetScores(string query)
    {
        if (_last is { } last && string.Equals(last.Item1, query, StringComparison.Ordinal))
            return last.Item2;

        lock (_lock)
        {
            if (_byQuery.TryGetValue(query, out var node))
            {
                _recent.Remove(node);
                _recent.AddFirst(node);
            }
            else
            {
                node = _recent.AddFirst((query, new()));
                _byQuery[query] = node;
                while (_recent.Count > MaxCachedQueries && _recent.Last is { } oldest)
                {
                    _recent.RemoveLast();
                    _byQuery.Remove(oldest.Value.Query);
                }
            }

            _last = Tuple.Create(query, node.Value.Scores);
            return node.Value.Scores;
        }
    }

    private static (bool, int, double, int)? ComputeFuzzyScore(string query, IReadOnlySet<string> names)
    {
        var normalizedQuery = SeriesSearch.NormalizeForIndex(query);
        if (string.IsNullOrWhiteSpace(normalizedQuery))
            return null;

        var isLatin = SeriesSearch.IsLatinScript(normalizedQuery);
        var maxErrors = SeriesSearch.GetMaxErrors(normalizedQuery.Length);
        (bool isNotExact, int index, double distance, int lengthDiff)? best = null;

        foreach (var name in names)
        {
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var normalizedName = SeriesSearch.NormalizeForIndex(name);
            if (string.IsNullOrWhiteSpace(normalizedName))
                continue;

            (bool isNotExact, int index, double distance, int lengthDiff)? current = null;

            if (isLatin)
            {
                if (!SeriesSearch.IsLatinScript(normalizedName))
                    continue;

                var idx = normalizedName.IndexOf(normalizedQuery, StringComparison.Ordinal);
                if (idx >= 0)
                {
                    // Prefix matches (index 0) are always top-quality; lengthDiff only differentiates non-prefix exact matches.
                    var diff = idx == 0 ? 0 : Math.Abs(normalizedQuery.Length - normalizedName.Length);
                    current = (false, idx, 0.0, diff);
                }
                else if (maxErrors > 0 && normalizedName.Length >= normalizedQuery.Length - maxErrors)
                {
                    var dist = SeriesSearch.MinEditDistInText(normalizedQuery, normalizedName);
                    if (dist <= maxErrors)
                        current = (true, 0, normalizedQuery.Length > 0 ? (double)dist / normalizedQuery.Length : 0.0, Math.Abs(normalizedQuery.Length - normalizedName.Length));
                }
            }
            else
            {
                var idx = normalizedName.IndexOf(normalizedQuery, StringComparison.Ordinal);
                if (idx >= 0)
                {
                    var diff = idx == 0 ? 0 : Math.Abs(normalizedQuery.Length - normalizedName.Length);
                    current = (false, idx, 0.0, diff);
                }
            }

            if (current is null)
                continue;

            if (best is null || Comparer<(bool, int, double, int)>.Default.Compare(current.Value, best.Value) < 0)
                best = current;
        }

        return best;
    }
}
