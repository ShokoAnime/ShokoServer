using System;
using Shoko.Abstractions.User;

namespace Shoko.Abstractions.Filtering.Events;

/// <summary>
///   Dispatched when a filter preset is stored, updated or deleted.
/// </summary>
public class FilterPresetEventArgs : EventArgs
{
    /// <summary>
    ///   The filter preset, as it is after the change. A deleted one is as it
    ///   was before.
    /// </summary>
    public required IFilterPreset Preset { get; init; }

    /// <summary>
    ///   The API token of whoever made the change, or <c>null</c> when the
    ///   system did it. Stamped when the event is raised.
    /// </summary>
    public ApiToken? Actor { get; init; }
}
