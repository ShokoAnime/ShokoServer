using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Shoko.Abstractions.Filtering.Services;
using Shoko.Server.Utilities;

namespace Shoko.Server.Filters;

public class FuzzySearchService : IFuzzySearchService
{
    private volatile ConditionalWeakTable<IReadOnlySet<string>, ConcurrentDictionary<string, (bool, int, double, int)?>> _scoreCache = new();

    /// <inheritdoc/>
    public void InvalidateCache()
        => _scoreCache = new ConditionalWeakTable<IReadOnlySet<string>, ConcurrentDictionary<string, (bool, int, double, int)?>>();

    /// <inheritdoc/>
    public bool FuzzyMatchesAnyName(string query, IReadOnlySet<string> names)
        => FuzzyScoreAnyName(query, names) is not null;

    /// <inheritdoc/>
    public (bool isNotExact, int index, double distance, int lengthDiff)? FuzzyScoreAnyName(string query, IReadOnlySet<string> names)
    {
        var cache = _scoreCache.GetOrCreateValue(names);
        return cache.GetOrAdd(query, q => ComputeFuzzyScore(q, names));
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
