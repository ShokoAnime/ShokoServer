using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Shoko;

namespace Shoko.Abstractions.Core.Update;

/// <summary>
///   Data for creating a new group. At least one series must be provided —
///   either directly in <see cref="Series"/> or via <see cref="Groups"/>
///   that contain series.
/// </summary>
public sealed class GroupData
{
    /// <summary>
    ///   The series to include directly in the group. Items are moved into the
    ///   new group. Either this or <see cref="Groups"/> must contain at least
    ///   one series.
    /// </summary>
    public IReadOnlyList<IShokoSeries> Series { get; set; } = [];

    /// <summary>
    ///   The child/sub-groups to include. Items are moved into the new group.
    ///   Either this or <see cref="Series"/> must contain at least one series.
    /// </summary>
    public IReadOnlyList<IShokoGroup> Groups { get; set; } = [];

    /// <summary>
    ///   The name of the group. If not set, it will be inferred from the
    ///   group's main series.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    ///   An optional description for the group. If not set, it will be
    ///   inferred from the group's main series.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///   An optional parent group to nest the new group under.
    /// </summary>
    public IShokoGroup? ParentGroup { get; set; }

    /// <summary>
    ///   The main series for the group.
    /// </summary>
    public IShokoSeries? MainSeries { get; set; }
}
