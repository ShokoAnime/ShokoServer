using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.User;

namespace Shoko.Abstractions.Filtering.Services;

/// <summary>
///   Responsible for evaluating filter presets on series or groups, and
///   returning the results. This is the core of the filtering engine in Shoko.
/// </summary>
public interface IFilterEvaluator
{
    /// <summary>
    ///   Evaluate the given filter, applying the necessary logic to each series
    ///   or group being evaluated to determine if it should be included in the
    ///   results. If a filter is time or user dependent then the user and time
    ///   must be provided, otherwise it will throw an exception.
    /// </summary>
    /// <param name="filter">
    ///   The filter to evaluate.
    /// </param>
    /// <param name="user">
    ///   The user. Needed if the filter is user-specific.
    /// </param>
    /// <param name="time">
    ///   The time. Needed if the filter is time-specific.
    /// </param>
    /// <param name="skipSorting">
    ///   By default the results are sorted using the filter's sort criteria,
    ///   setting this to true will skip sorting the results.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when <paramref name="filter"/> is <c>null</c>, or if
    ///   <paramref name="user"/> is <c>null</c> when the filter is user
    ///   dependent, or if
    ///   <paramref name="time"/> is <c>null</c> when the filter is time
    ///   dependent.
    /// </exception>
    /// <returns>SeriesIDs, grouped by the direct parent GroupID</returns>
    IReadOnlyList<IGrouping<int, int>> EvaluateFilter(IFilterPreset filter, IUser? user = null, DateTime? time = null, bool skipSorting = false);

    /// <summary>
    ///   Prepare the input filters, returning a dictionary which lazily
    ///   evaluates them when accessed.
    /// </summary>
    /// <typeparam name="TFilter">
    ///   The filter type.
    /// </typeparam>
    /// <param name="filters">
    ///   The filters to prepare.
    /// </param>
    /// <param name="user">
    ///   The user. Needed if the filters are user-specific.
    /// </param>
    /// <param name="time">
    ///   The time. Needed if the filters are time-specific.
    /// </param>
    /// <param name="skipSorting">
    ///   By default the results are sorted using the filter's sort criteria,
    ///   setting this to true will skip sorting the results.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when <paramref name="filters"/> is <c>null</c>, or if
    ///   <paramref name="user"/> is <c>null</c> when the filter is user
    ///   dependent, or if
    ///   <paramref name="time"/> is <c>null</c> when the filter is time
    ///   dependent.
    /// </exception>
    /// <returns>
    ///   A lazy dictionary of seriesIDs, grouped by the direct parent GroupID per filter.
    /// </returns>
    IReadOnlyDictionary<TFilter, IReadOnlyList<IGrouping<int, int>>> BatchPrepareFilters<TFilter>(IReadOnlyList<TFilter> filters, IUser? user = null, DateTime? time = null, bool skipSorting = false) where TFilter : IFilterPreset;
}
