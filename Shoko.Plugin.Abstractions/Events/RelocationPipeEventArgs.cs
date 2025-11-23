using System;
using Shoko.Plugin.Abstractions.Relocation;

namespace Shoko.Plugin.Abstractions.Events;

/// <summary>
///   Dispatched when a relocation pipe event is raised.
/// </summary>
public class RelocationPipeEventArgs : EventArgs
{
    /// <summary>
    /// The relocation pipe the event was raised for.
    /// </summary>
    public required IStoredRelocationPipe RelocationPipe { get; init; }
}
