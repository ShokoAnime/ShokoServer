using System;
using System.Collections.Generic;
using Shoko.Abstractions.Filtering.Services;
using Shoko.Server.Utilities;

namespace Shoko.Server.Filters;

public class FuzzySearchService : IFuzzySearchService
{
    /// <inheritdoc/>
    public bool FuzzyMatchesAnyName(string query, IReadOnlySet<string> names)
    {
        var normalizedQuery = SeriesSearch.NormalizeForIndex(query);
        if (string.IsNullOrWhiteSpace(normalizedQuery))
            return false;

        var isLatin = SeriesSearch.IsLatinScript(normalizedQuery);
        var maxErrors = SeriesSearch.GetMaxErrors(normalizedQuery.Length);

        foreach (var name in names)
        {
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var normalizedName = SeriesSearch.NormalizeForIndex(name);
            if (string.IsNullOrWhiteSpace(normalizedName))
                continue;

            if (isLatin)
            {
                if (!SeriesSearch.IsLatinScript(normalizedName))
                    continue;

                if (normalizedName.Contains(normalizedQuery, StringComparison.Ordinal))
                    return true;

                if (maxErrors > 0 && normalizedName.Length >= normalizedQuery.Length - maxErrors)
                {
                    var dist = SeriesSearch.MinEditDistInText(normalizedQuery, normalizedName);
                    if (dist <= maxErrors)
                        return true;
                }
            }
            else
            {
                if (normalizedName.Contains(normalizedQuery, StringComparison.Ordinal))
                    return true;
            }
        }

        return false;
    }
}
