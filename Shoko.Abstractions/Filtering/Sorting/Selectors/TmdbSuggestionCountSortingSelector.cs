using System;
using Shoko.Abstractions.Metadata;

namespace Shoko.Abstractions.Filtering.Sorting.Selectors;

/// <summary>
/// This sorts by the number of suggestions TMDB makes for a filterable. Counts what the filterable suggests, not what suggests it.
/// </summary>
/// <remarks>
/// The TMDB form of <see cref="SourceSuggestionCountSortingSelector"/>,
/// which it evaluates with the tmdb source. Kept under its own name so
/// saved filters read the same.
/// </remarks>
public class TmdbSuggestionCountSortingSelector : SortingExpression
{
    private static readonly SourceSuggestionCountSortingSelector _tmdb = new(MetadataSource.TMDB.Value);

    /// <inheritdoc/>
    public override string HelpDescription => "This sorts by the number of suggestions TMDB makes for a filterable";

    /// <inheritdoc/>
    public override object Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return _tmdb.Evaluate(filterable, userInfo, time);
    }
}
