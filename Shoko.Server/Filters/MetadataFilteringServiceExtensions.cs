using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Filtering;
using Shoko.Abstractions.Filtering.Services;
using Shoko.Abstractions.User;

namespace Shoko.Server.Filters;

/// <summary>
///   Narrows reads outside the filtering system by a filter.
/// </summary>
public static class MetadataFilteringServiceExtensions
{
    /// <summary>
    ///   Evaluates a filter for a user, and gives the AniDB anime of the
    ///   Shoko series it passes: in the filter's order when it has a sorting
    ///   expression, else in no particular order.
    /// </summary>
    /// <param name="filteringService">The filtering service.</param>
    /// <param name="filter">The filter.</param>
    /// <param name="user">The user, needed when the filter depends on one.</param>
    /// <exception cref="ArgumentNullException">
    ///   The filter depends on the user and <paramref name="user"/> is <c>null</c>.
    /// </exception>
    /// <returns>The AniDB anime IDs, each once.</returns>
    public static IReadOnlyList<int> GetFilteredAnimeIDs(this IMetadataFilteringService filteringService, IFilter filter, IUser? user)
        =>
        [
            .. filteringService.GetAllFilteredSeries(filter, user, skipSorting: filter.SortingExpression is null)
                .Select(series => series.AnidbAnimeID)
                .Distinct(),
        ];
}
