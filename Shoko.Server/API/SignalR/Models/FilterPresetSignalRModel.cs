using Shoko.Abstractions.Filtering.Events;

namespace Shoko.Server.API.SignalR.Models;

/// <summary>
/// Dispatched when a filter preset is stored, updated or deleted.
/// </summary>
/// <param name="args">The event arguments.</param>
public class FilterPresetSignalRModel(FilterPresetEventArgs args)
{
    /// <summary>
    /// The filter ID.
    /// </summary>
    public int FilterID { get; } = args.Preset.LocalID;

    /// <summary>
    /// The ID of the directory filter it is in, or <c>null</c> at the top.
    /// </summary>
    public int? ParentFilterID { get; } = args.Preset.ParentFilterID;

    /// <summary>
    /// The filter's name.
    /// </summary>
    public string Name { get; } = args.Preset.Name;

    /// <summary>
    /// Whether the filter is a directory of other filters.
    /// </summary>
    public bool IsDirectory { get; } = args.Preset.IsDirectory;
}
