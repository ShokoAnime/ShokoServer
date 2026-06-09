using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shoko.Abstractions.Core.Update;
using Shoko.Abstractions.Exceptions;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Events;
using Shoko.Abstractions.Metadata.Shoko;

namespace Shoko.Abstractions.Metadata.Services;

/// <summary>
///   Provides group management capabilities for Shoko groups, including creating,
///   updating, and deleting groups, as well as moving series between groups.
/// </summary>
public interface IShokoGroupManager
{
    #region Events

    /// <summary>
    ///   Dispatched when a group has been added.
    /// </summary>
    event EventHandler<GroupInfoUpdatedEventArgs>? GroupAdded;

    /// <summary>
    ///   Dispatched when a group has been updated.
    /// </summary>
    event EventHandler<GroupInfoUpdatedEventArgs>? GroupUpdated;

    /// <summary>
    ///   Dispatched when a group has been removed.
    /// </summary>
    event EventHandler<GroupInfoUpdatedEventArgs>? GroupRemoved;

    /// <summary>
    ///   Dispatched when a series has been moved between groups.
    /// </summary>
    event EventHandler<SeriesMovedEventArgs>? SeriesMoved;

    /// <summary>
    ///   Dispatched when all groups have been recreated.
    /// </summary>
    event EventHandler? GroupsRecreated;

    #endregion

    #region CRUD

    /// <summary>
    ///   Gets all groups.
    /// </summary>
    /// <returns>All groups.</returns>
    IEnumerable<IShokoGroup> GetAllGroups();

    /// <summary>
    ///   Gets a group by ID.
    /// </summary>
    /// <param name="groupID">The group ID.</param>
    /// <returns>The group, or <c>null</c> if not found.</returns>
    IShokoGroup? GetGroupByID(int groupID);

    /// <summary>
    ///   Creates a new group from the specified data. At least one series must be
    ///   provided — either directly or via child groups. The name and description
    ///   are optional; if omitted they are inferred from the group's main series.
    /// </summary>
    /// <param name="data">The creation data.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is <c>null</c>.</exception>
    /// <exception cref="GenericValidationException">Thrown when <paramref name="data"/> fails one or more validation rules.</exception>
    /// <returns>The newly created group.</returns>
    IShokoGroup CreateGroup(GroupData data);

    /// <summary>
    ///   Sets the main series for a group.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <param name="series">The series to set as the main series, or <c>null</c> to clear.</param>
    /// <exception cref="GenericValidationException">Thrown when the series lies outside the group.</exception>
    void SetMainSeries(IShokoGroup group, IShokoSeries? series);

    /// <summary>
    ///   Moves a series from its current group to the target group.
    /// </summary>
    /// <param name="series">The series to move.</param>
    /// <param name="targetGroup">The group to move the series to.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="series"/> or <paramref name="targetGroup"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="series"/> or <paramref name="targetGroup"/> is not an <c>AnimeSeries</c> or <c>AnimeGroup</c> instance.</exception>
    void MoveSeries(IShokoSeries series, IShokoGroup targetGroup);

    /// <summary>
    ///   Updates an existing group with the specified changes.
    /// </summary>
    /// <param name="group">The group to update.</param>
    /// <param name="updateData">The updates to apply to the group.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="group"/> or <paramref name="updateData"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="group"/> is not an <c>AnimeGroup</c> instance.</exception>
    /// <exception cref="GenericValidationException">Thrown when <paramref name="updateData"/> fails one or more validation rules.</exception>
    /// <returns>The updated group.</returns>
    IShokoGroup UpdateGroup(IShokoGroup group, GroupUpdateData updateData);

    /// <summary>
    ///   Deletes a group, optionally also deleting its series and files.
    /// </summary>
    /// <param name="group">The group to delete.</param>
    /// <param name="deleteSeries">
    ///   If <c>true</c>, all series within the group will be deleted first. If
    ///   <c>false</c>, all series will be moved to new, separated groups.
    /// </param>
    /// <param name="deleteFiles">
    ///   If <c>true</c>, the files associated with the series will also be deleted
    ///   from disk. Only relevant when <paramref name="deleteSeries"/> is <c>true</c>.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="group"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="group"/> is not an <c>AnimeGroup</c> instance.</exception>
    Task DeleteGroup(IShokoGroup group, bool deleteSeries = false, bool deleteFiles = false);

    #endregion

    #region Auto-Grouping

    /// <summary>
    ///   Indicates that auto-grouping is enabled or disabled.
    /// </summary>
    bool IsAutoGroupingEnabled { get; set; }

    /// <summary>
    ///   Indicates that relation weighting should be used to determine the main
    ///   series instead of the air date.
    /// </summary>
    bool UseAutoGroupingRelationWeighting { get; set; }

    /// <summary>
    ///   The set of relation types to exclude when auto-grouping.
    /// </summary>
    IReadOnlySet<RelationType> AutoGroupingRelationExclusions { get; set; }

    /// <summary>
    ///   Allow titles that are not similar to be grouped together.
    /// </summary>
    bool AllowDissimilarTitleExclusion { get; set; }

    /// <summary>
    ///   Recreates all groups using the current auto-grouping settings.
    /// </summary>
    Task RecreateAllGroups();

    #endregion

    #region Management

    /// <summary>
    ///   Renames all groups to match their main series.
    /// </summary>
    void RenameAllGroups();

    #endregion
}
