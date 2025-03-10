using System;
using Shoko.Plugin.Abstractions.DataModels;

namespace Shoko.Plugin.Abstractions.Events;

/// <summary>
/// Dispatched when a managed folder is added,
/// updated, or removed.
/// </summary>
public class ManagedFolderChangedEventArgs : EventArgs
{
    /// <summary>
    /// The folder that was added/updated/removed.
    /// </summary>
    public required IManagedFolder Folder { get; init; }
}
