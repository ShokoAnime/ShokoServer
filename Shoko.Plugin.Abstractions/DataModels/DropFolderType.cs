using System;

namespace Shoko.Plugin.Abstractions.DataModels;

/// <summary>
/// How an <see cref="IManagedFolder"/> is used in the rename/move system.
/// </summary>
[Flags]
public enum DropFolderType
{
    /// <summary>
    /// Excluded from use in the rename/move system.
    /// </summary>
    Excluded = 0,

    /// <summary>
    /// A drop source in the rename/move system.
    /// </summary>
    Source = 1,

    /// <summary>
    /// A drop destination in the rename/move system.
    /// </summary>
    Destination = 2,

    /// <summary>
    /// A drop source and destination in the rename/move system.
    /// </summary>
    Both = Source | Destination,
}
