using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Shoko;

namespace Shoko.Abstractions.Core.Update;

/// <summary>
///   Represents an update to a group. Leave a property at its default value
///   to leave it unchanged. For flagged properties (<see cref="HasName"/>,
///   <see cref="HasOverview"/>, <see cref="HasParentGroup"/>,
///   <see cref="HasMainSeries"/>), set the value to explicitly indicate
///   intent — including setting the corresponding property to <c>null</c> to
///   clear or reset it.
/// </summary>
public sealed class GroupUpdateData
{
    /// <summary>
    ///   The child groups to move into the group. Items are moved into the
    ///   group; existing child groups are never removed. Leave at the default
    ///   value to not change the set of child groups.
    /// </summary>
    public IReadOnlyList<IShokoGroup> Groups { get; set; } = [];

    /// <summary>
    ///   The series to move to the group. Items are moved into the group;
    ///   existing series are never removed. Leave at the default value to not
    ///   change the series membership.
    /// </summary>
    public IReadOnlyList<IShokoSeries> Series { get; set; } = [];

    private string? _name;

    /// <summary>
    ///   Indicates that <see cref="Name"/> has been explicitly set (including to
    ///   <c>null</c> to reset to automatic naming).
    /// </summary>
    public bool HasName { get; private set; }

    /// <summary>
    ///   The group's new name. Set to <c>null</c> to reset to automatic naming
    ///   based on the main series. Only applied when <see cref="HasName"/> is
    ///   <c>true</c>.
    /// </summary>
    public string? Name
    {
        get => _name;
        set { HasName = true; _name = value; }
    }

    private string? _overview;

    /// <summary>
    ///   Indicates that <see cref="Overview"/> has been explicitly set
    ///   (including to <c>null</c> to reset to the automatic overview).
    /// </summary>
    public bool HasOverview { get; private set; }

    /// <summary>
    ///   The group's new overview. Set to <c>null</c> to reset to the automatic
    ///   overview based on the main series. Only applied when
    ///   <see cref="HasOverview"/> is <c>true</c>.
    /// </summary>
    public string? Overview
    {
        get => _overview;
        set { HasOverview = true; _overview = value; }
    }

    /// <summary>
    ///   Indicates that <see cref="Overview"/> has been explicitly set.
    /// </summary>
    [Obsolete("Use HasOverview instead.")]
    public bool HasDescription { get => HasOverview; }

    /// <summary>
    ///   The group's new description.
    /// </summary>
    [Obsolete("Use Overview instead.")]
    public string? Description { get => Overview; set => Overview = value; }

    private IShokoGroup? _parentGroup;

    /// <summary>
    ///   Indicates that <see cref="ParentGroup"/> has been explicitly set
    ///   (including to <c>null</c> to remove the group from its parent).
    /// </summary>
    public bool HasParentGroup { get; private set; }

    /// <summary>
    ///   The parent group to move the group under. Set to a group to set it, or
    ///   to <c>null</c> to remove the group from its current parent. Only applied
    ///   when <see cref="HasParentGroup"/> is <c>true</c>.
    /// </summary>
    public IShokoGroup? ParentGroup
    {
        get => _parentGroup;
        set { HasParentGroup = true; _parentGroup = value; }
    }

    private IShokoSeries? _mainSeries;

    /// <summary>
    ///   Indicates that <see cref="MainSeries"/> has been explicitly set
    ///   (including to <c>null</c> to clear it for auto-detection).
    /// </summary>
    public bool HasMainSeries { get; private set; }

    /// <summary>
    ///   The main series for the group. Set to a series to set it, or to
    ///   <c>null</c> to clear it for auto-detection. Only applied when
    ///   <see cref="HasMainSeries"/> is <c>true</c>.
    /// </summary>
    public IShokoSeries? MainSeries
    {
        get => _mainSeries;
        set { HasMainSeries = true; _mainSeries = value; }
    }
}
