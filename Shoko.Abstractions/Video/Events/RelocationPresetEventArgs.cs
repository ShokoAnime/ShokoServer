using System;
using Shoko.Abstractions.Video.Relocation;

namespace Shoko.Abstractions.Video.Events;

/// <summary>
///   Dispatched when a relocation preset event is raised.
/// </summary>
public class RelocationPresetEventArgs : EventArgs
{
    /// <summary>
    /// The relocation preset the event was raised for.
    /// </summary>
    public required IStoredRelocationPreset RelocationPreset { get; init; }
}
