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

    /// <summary>
    /// Returns the best relevance score for <paramref name="query"/> against
    /// <paramref name="names"/>, or <c>null</c> if no name matches. Lower tuple
    /// values compare first, so ascending sort puts best matches at the top.
    /// Elements: <c>isNotExact</c> (false = exact substring match), <c>index</c>
    /// (match position; lower = better), <c>distance</c> (normalised edit
    /// distance; 0.0 = exact), <c>lengthDiff</c> (absolute length difference).
    /// </summary>
    (bool isNotExact, int index, double distance, int lengthDiff)? FuzzyScoreAnyName(string query, IReadOnlySet<string> names);

    /// <summary>
    /// Clears any internal score cache. Call when the preferred-language
    /// order changes so stale scores are recomputed on next evaluation.
    /// </summary>
    void InvalidateCache();
}
