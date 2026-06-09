using System;
using System.Collections.Generic;
using Shoko.Abstractions.Filtering.Expressions;
using Shoko.Abstractions.Filtering.Models;
using Shoko.Abstractions.Filtering.Sorting;

namespace Shoko.Abstractions.Filtering.Services;

/// <summary>
///   Manages filter presets: CRUD operations and expression/selector discovery.
/// </summary>
public interface IFilterPresetManager
{
    /// <summary>
    ///   Get all top-level filter presets.
    /// </summary>
    /// <returns>
    ///   The top-level filter presets.
    /// </returns>
    IReadOnlyList<IFilterPreset> GetTopLevelPresets();

    /// <summary>
    ///   Get a filter preset by its ID.
    /// </summary>
    /// <param name="filterID">
    ///   The filter preset ID.
    /// </param>
    /// <returns>
    ///   The filter preset.
    /// </returns>
    IFilterPreset? GetPresetById(int filterID);

    /// <summary>
    ///   Get all sub-filters of a parent filter preset.
    /// </summary>
    /// <param name="filterPreset">
    ///   The parent filter preset.
    /// </param>
    /// <returns>
    ///   The sub-filters.
    /// </returns>
    IReadOnlyList<IFilterPreset> GetPresetsByParentPreset(IFilterPreset filterPreset);

    /// <summary>
    ///   Create a new filter preset.
    /// </summary>
    /// <param name="data">
    ///   The input data.
    /// </param>
    IFilterPreset CreatePreset(FilterPresetData data);

    /// <summary>
    ///   Update an existing filter preset.
    /// </summary>
    /// <param name="filterPreset">
    ///   The filter preset to update.
    /// </param>
    /// <param name="updateData">
    ///   The update data.
    /// </param>
    /// <returns>
    ///   The updated filter preset.
    /// </returns>
    IFilterPreset UpdatePreset(IFilterPreset filterPreset, FilterPresetUpdateData updateData);

    /// <summary>
    ///   Delete a filter preset.
    /// </summary>
    /// <param name="filterPreset">
    ///   The filter preset to delete.
    /// </param>
    void DeletePreset(IFilterPreset filterPreset);

    /// <summary>
    ///   Get help metadata for the given filter expression.
    /// </summary>
    /// <typeparam name="T">
    ///   The type of the filter expression.
    /// </typeparam>
    /// <returns>
    ///   The help metadata.
    /// </returns>
    IFilterExpressionHelp GetHelpForFilterType<T>() where T : FilterExpression;

    /// <summary>
    ///   Get help metadata for the given filter expression.
    /// </summary>
    /// <param name="filterType">
    ///   The type of the filter expression.
    /// </param>
    /// <returns>
    ///   The help metadata.
    /// </returns>
    IFilterExpressionHelp? GetHelpForFilterType(Type filterType);

    /// <summary>
    ///   Get help metadata for available filter expressions.
    /// </summary>
    /// <param name="group">
    ///   Optional. If provided, will scope the returned expressions to only
    ///   expressions from the given group.
    /// </param>
    IReadOnlyList<IFilterExpressionHelp> GetAvailableFilterExpressions(FilterExpressionGroup? group = null);

    /// <summary>
    ///   Get help metadata for the given sorting criteria.
    /// </summary>
    /// <typeparam name="T">
    ///   The type of the sorting expression.
    /// </typeparam>
    /// <returns>
    ///   The help metadata.
    /// </returns>
    ISortingExpressionHelp GetHelpForSortingType<T>() where T : SortingExpression;

    /// <summary>
    ///   Get help metadata for the given sorting criteria.
    /// </summary>
    /// <param name="sortingType">
    ///   The type of the sorting expression.
    /// </param>
    /// <returns>
    ///   The help metadata.
    /// </returns>
    ISortingExpressionHelp? GetHelpForSortingType(Type sortingType);

    /// <summary>
    ///   Get help metadata for available sorting expressions.
    /// </summary>
    IReadOnlyList<ISortingExpressionHelp> GetAvailableSortingExpressions();
}
