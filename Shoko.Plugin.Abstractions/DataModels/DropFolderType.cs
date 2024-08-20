using System;

namespace Shoko.Plugin.Abstractions.DataModels;

/// <summary>
/// The rules that this Import Folder should adhere to. A folder that is both a Source and Destination cares not how files are moved in or out of it.
/// </summary>
[Flags]
public enum DropFolderType
{
    /// <summary>
    /// None.
    /// </summary>
    Excluded = 0,

    /// <summary>
    /// Source.
    /// </summary>
    Source = 1,

    /// <summary>
    /// Destination.
    /// </summary>
    Destination = 2,

    /// <summary>
    /// Both Source and Destination.
    /// </summary>
    Both = Source | Destination,
}
