
using System.Collections.Generic;

namespace Shoko.Plugin.Abstractions.DataModels.Shoko;

/// <summary>
/// Shoko group metadata.
/// </summary>
public interface IShokoGroup : IWithTitles, IWithDescriptions, IMetadata<int>
{
    /// <summary>
    /// The id of the direct parent group if the group is a child-group.
    /// </summary>
    int? ParentGroupID { get; }

    /// <summary>
    /// The id of the top-level group this group belongs to. It can refer to
    /// itself if it is atop-level group.
    /// </summary>
    int TopLevelGroupID { get; }

    /// <summary>
    /// The main series id for the group, be it automatically selected or
    /// a configured by a user.
    /// </summary>
    int MainSeriesID { get; }

    /// <summary>
    /// Indicates that the user have configured a main series for the group set.
    /// </summary>
    bool HasConfiguredMainSeries { get; }

    /// <summary>
    /// Indicates that the group has a custom title set.
    /// </summary>
    bool HasCustomTitle { get; }

    /// <summary>
    /// Indicates that the group have a custom description set.
    /// </summary>
    bool HasCustomDescription { get; }

    /// <summary>
    /// The direct parent of the group if the group is a child-group.
    /// </summary>
    IShokoGroup? ParentGroup { get; }

    /// <summary>
    /// The top-level group this group belongs to. It can refer to itself if it
    /// is a top-level group.
    /// </summary>
    IShokoGroup TopLevelGroup { get; }

    /// <summary>
    /// All child groups directly within the group, unordered.
    /// </summary>
    IReadOnlyList<IShokoGroup> Groups { get; }

    /// <summary>
    /// All child groups directly within the group and within all child groups,
    /// unordered.
    /// </summary>
    IReadOnlyList<IShokoGroup> AllGroups { get; }

    /// <summary>
    /// The main series within the group. It can be auto-selected (when
    /// auto-grouping is enabled) or user overwritten, and will fallback to the
    /// earliest airing series within the group or any child-groups if nothing
    /// is selected.
    /// </summary>
    IShokoSeries MainSeries { get; }

    /// <summary>
    /// The series directly within the group, ordered by air-date.
    /// </summary>
    IReadOnlyList<IShokoSeries> Series { get; }

    /// <summary>
    /// All series directly within the group and within all child-groups (if
    /// any), ordered by air-date.
    /// </summary>
    IReadOnlyList<IShokoSeries> AllSeries { get; }
}
