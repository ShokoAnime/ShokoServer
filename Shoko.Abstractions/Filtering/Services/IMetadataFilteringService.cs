using System;
using System.Collections.Generic;
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

    /// <summary>
    ///   Evaluate the given filter and return matching groups.
    /// </summary>
    /// <param name="filter">The filter to evaluate.</param>
    /// <param name="user">The user. Needed if the filter is user-specific.</param>
    /// <param name="time">The time. Needed if the filter is time-specific.</param>
    /// <param name="skipSorting">Skip sorting the results.</param>
    /// <returns>A list of matching groups.</returns>
    IReadOnlyList<IShokoGroup> FilterGroups(IFilterPreset filter, IUser? user = null, DateTime? time = null, bool skipSorting = false);

    /// <summary>
    ///   Evaluate the given filter and return matching series.
    /// </summary>
    /// <param name="filter">The filter to evaluate.</param>
    /// <param name="user">The user. Needed if the filter is user-specific.</param>
    /// <param name="time">The time. Needed if the filter is time-specific.</param>
    /// <param name="skipSorting">Skip sorting the results.</param>
    /// <returns>A list of matching series.</returns>
    IReadOnlyList<IShokoSeries> FilterSeries(IFilterPreset filter, IUser? user = null, DateTime? time = null, bool skipSorting = false);

    /// <summary>
    ///   Batch evaluate multiple filters and return matching groups per filter.
    /// </summary>
    /// <typeparam name="TFilter">The filter type.</typeparam>
    /// <param name="filters">The filters to evaluate.</param>
    /// <param name="user">The user. Needed if the filters are user-specific.</param>
    /// <param name="time">The time. Needed if the filters are time-specific.</param>
    /// <param name="skipSorting">Skip sorting the results.</param>
    /// <returns>A dictionary mapping each filter to its matching groups.</returns>
    IReadOnlyDictionary<TFilter, IReadOnlyList<IShokoGroup>> BatchFilterGroups<TFilter>(IReadOnlyList<TFilter> filters, IUser? user = null, DateTime? time = null, bool skipSorting = false) where TFilter : IFilterPreset;

    /// <summary>
    ///   Batch evaluate multiple filters and return matching series per filter.
    /// </summary>
    /// <typeparam name="TFilter">The filter type.</typeparam>
    /// <param name="filters">The filters to evaluate.</param>
    /// <param name="user">The user. Needed if the filters are user-specific.</param>
    /// <param name="time">The time. Needed if the filters are time-specific.</param>
    /// <param name="skipSorting">Skip sorting the results.</param>
    /// <returns>A dictionary mapping each filter to its matching series.</returns>
    IReadOnlyDictionary<TFilter, IReadOnlyList<IShokoSeries>> BatchFilterSeries<TFilter>(IReadOnlyList<TFilter> filters, IUser? user = null, DateTime? time = null, bool skipSorting = false) where TFilter : IFilterPreset;
}
