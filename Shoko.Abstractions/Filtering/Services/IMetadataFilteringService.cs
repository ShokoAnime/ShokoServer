using System;
using System.Collections.Generic;
using System.Threading;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.User;

namespace Shoko.Abstractions.Filtering.Services;

/// <summary>
///   Higher-level filtering service that wraps <see cref="IFilteringEngine"/>
///   and exposes typed results with <see cref="IShokoGroup"/> and
///   <see cref="IShokoSeries"/> instead of raw tuples/groupings.
/// </summary>
public interface IMetadataFilteringService
{
    /// <summary>
    ///   The underlying filtering engine.
    /// </summary>
    IFilteringEngine Engine { get; }

    #region Group

    /// <summary>
    ///   Evaluate the given filter and return matching groups.
    /// </summary>
    /// <param name="filter">The filter to evaluate.</param>
    /// <param name="user">The user. Needed if the filter is user-specific.</param>
    /// <param name="time">The time. Needed if the filter is time-specific.</param>
    /// <param name="skipSorting">Skip sorting the results.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <exception cref="OperationCanceledException">
    ///   Thrown when <paramref name="cancellationToken"/> is cancelled.
    /// </exception>
    /// <returns>A list of matching groups.</returns>
    IReadOnlyList<IShokoGroup> GetAllFilteredGroups(
        IFilter filter,
        IUser? user = null,
        DateTime? time = null,
        bool skipSorting = false,
        CancellationToken cancellationToken = default
    );
    /// <summary>
    ///   Evaluate the filter and return matching groups with hierarchy chain
    ///   information. Each result contains the group, the chain of group IDs
    ///   from top to itself, and the set of series IDs that matched within
    ///   that scope.
    /// </summary>
    /// <param name="filter">The filter to evaluate.</param>
    /// <param name="user">The user. Needed if the filter is user-specific.</param>
    /// <param name="time">The time. Needed if the filter is time-specific.</param>
    /// <param name="skipSorting">Skip sorting the results.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <exception cref="OperationCanceledException">
    ///   Thrown when <paramref name="cancellationToken"/> is cancelled.
    /// </exception>
    /// <returns>A list of filtered group results with hierarchy chain information.</returns>
    IReadOnlyList<FilteredGroupResult> GetAllFilteredGroupsWithChains(
        IFilter filter,
        IUser? user = null,
        DateTime? time = null,
        bool skipSorting = false,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    ///   Evaluate the filter and return groups resolved to their top-level
    ///   ancestor with hierarchy chain information. Each result contains the
    ///   top-level group, the chain of group IDs from top to the matching
    ///   sub-group, and the set of series IDs that matched within that scope.
    /// </summary>
    /// <param name="filter">The filter to evaluate.</param>
    /// <param name="user">The user. Needed if the filter is user-specific.</param>
    /// <param name="time">The time. Needed if the filter is time-specific.</param>
    /// <param name="skipSorting">Skip sorting the results.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <exception cref="OperationCanceledException">
    ///   Thrown when <paramref name="cancellationToken"/> is cancelled.
    /// </exception>
    /// <returns>A list of filtered group results resolved to top-level.</returns>
    IReadOnlyList<FilteredGroupResult> GetTopLevelFilteredGroups(
        IFilter filter,
        IUser? user = null,
        DateTime? time = null,
        bool skipSorting = false,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    ///   Evaluate the filter and return only groups that are direct children of
    ///   the specified parent group, with hierarchy chain information.
    /// </summary>
    /// <param name="filter">The filter to evaluate.</param>
    /// <param name="parentGroup">The parent group to scope results to.</param>
    /// <param name="user">The user. Needed if the filter is user-specific.</param>
    /// <param name="time">The time. Needed if the filter is time-specific.</param>
    /// <param name="skipSorting">Skip sorting the results.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <exception cref="OperationCanceledException">
    ///   Thrown when <paramref name="cancellationToken"/> is cancelled.
    /// </exception>
    /// <returns>A list of filtered group results within the parent group.</returns>
    IReadOnlyList<FilteredGroupResult> GetFilteredSubGroups(
        IFilter filter,
        IShokoGroup parentGroup,
        IUser? user = null,
        DateTime? time = null,
        bool skipSorting = false,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    ///   Batch evaluate multiple filters and return matching groups per filter.
    /// </summary>
    /// <typeparam name="TFilter">The filter type.</typeparam>
    /// <param name="filters">The filters to evaluate.</param>
    /// <param name="user">The user. Needed if the filters are user-specific.</param>
    /// <param name="time">The time. Needed if the filters are time-specific.</param>
    /// <param name="skipSorting">Skip sorting the results.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <exception cref="OperationCanceledException">
    ///   Thrown when <paramref name="cancellationToken"/> is cancelled.
    /// </exception>
    /// <returns>A dictionary mapping each filter to its matching groups.</returns>
    IReadOnlyDictionary<TFilter, IReadOnlyList<IShokoGroup>> BatchFilterGroups<TFilter>(
        IReadOnlyList<TFilter> filters,
        IUser? user = null,
        DateTime? time = null,
        bool skipSorting = false,
        CancellationToken cancellationToken = default
    ) where TFilter : IFilter;

    #endregion

    #region Series

    /// <summary>
    ///   Evaluate the given filter and return matching series.
    /// </summary>
    /// <param name="filter">The filter to evaluate.</param>
    /// <param name="user">The user. Needed if the filter is user-specific.</param>
    /// <param name="time">The time. Needed if the filter is time-specific.</param>
    /// <param name="skipSorting">Skip sorting the results.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <exception cref="OperationCanceledException">
    ///   Thrown when <paramref name="cancellationToken"/> is cancelled.
    /// </exception>
    /// <returns>A list of matching series.</returns>
    IReadOnlyList<IShokoSeries> GetAllFilteredSeries(
        IFilter filter,
        IUser? user = null,
        DateTime? time = null,
        bool skipSorting = false,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    ///   Evaluate the filter and return series IDs that belong to the specified
    ///   group (and its descendants if <paramref name="recursive"/> is <c>true</c>).
    /// </summary>
    /// <param name="filter">The filter to evaluate.</param>
    /// <param name="group">The group to scope results to.</param>
    /// <param name="recursive">Include series from sub-groups.</param>
    /// <param name="user">The user. Needed if the filter is user-specific.</param>
    /// <param name="time">The time. Needed if the filter is time-specific.</param>
    /// <param name="skipSorting">Skip sorting the results.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <exception cref="OperationCanceledException">
    ///   Thrown when <paramref name="cancellationToken"/> is cancelled.
    /// </exception>
    /// <returns>A list of series IDs matching the filter within the group scope.</returns>
    IReadOnlyList<IShokoSeries> GetFilteredSeriesInGroup(
        IFilter filter,
        IShokoGroup group,
        bool recursive = false,
        IUser? user = null,
        DateTime? time = null,
        bool skipSorting = false,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    ///   Batch evaluate multiple filters and return matching series per filter.
    /// </summary>
    /// <typeparam name="TFilter">The filter type.</typeparam>
    /// <param name="filters">The filters to evaluate.</param>
    /// <param name="user">The user. Needed if the filters are user-specific.</param>
    /// <param name="time">The time. Needed if the filters are time-specific.</param>
    /// <param name="skipSorting">Skip sorting the results.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <exception cref="OperationCanceledException">
    ///   Thrown when <paramref name="cancellationToken"/> is cancelled.
    /// </exception>
    /// <returns>A dictionary mapping each filter to its matching series.</returns>
    IReadOnlyDictionary<TFilter, IReadOnlyList<IShokoSeries>> BatchFilterSeries<TFilter>(
        IReadOnlyList<TFilter> filters,
        IUser? user = null,
        DateTime? time = null,
        bool skipSorting = false,
        CancellationToken cancellationToken = default
    ) where TFilter : IFilter;

    #endregion
}
