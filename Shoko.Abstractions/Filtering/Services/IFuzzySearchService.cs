using System.Collections.Generic;

namespace Shoko.Abstractions.Filtering.Services;

/// <summary>
/// Evaluates fuzzy name matches for filter expressions. The implementation
/// lives server-side; the expression in Abstractions resolves it at runtime
/// via <see cref="Core.Services.ISystemService.StaticServices"/>.
/// </summary>
public interface IFuzzySearchService
{
    /// <summary>
    /// Returns <c>true</c> if <paramref name="query"/> fuzzy-matches at least
    /// one entry in <paramref name="names"/>. Matching uses culture-normalized
    /// trigram indexing for Latin input and a substring fallback for non-Latin
    /// (CJK, Arabic, Cyrillic, etc.) input.
    /// </summary>
    bool FuzzyMatchesAnyName(string query, IReadOnlySet<string> names);
}
