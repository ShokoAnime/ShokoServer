using System.Collections.Generic;
using System.ComponentModel;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.DataModels.Shoko;

namespace Shoko.Plugin.Abstractions.Events;

/// <summary>
/// Event Args for File Relocation
/// </summary>
public class RelocationEventArgs : CancelEventArgs
{
    /// <summary>
    /// If the settings have moving enabled
    /// </summary>
    public bool MoveEnabled { get; set; }

    /// <summary>
    /// If the settings have renaming enabled
    /// </summary>
    public bool RenameEnabled { get; set; }

    /// <summary>
    /// The available import folders to choose as a destination. You can set the <see cref="RelocationResult.DestinationImportFolder"/> to one of these.
    /// If a Folder has <see cref="DropFolderType.Excluded"/> set, then it won't be in this list.
    /// </summary>
    public IReadOnlyList<IImportFolder> AvailableFolders { get; set; }

    /// <summary>
    /// Information about the file and video, such as MediaInfo, current location, size, etc
    /// </summary>
    public IVideoFile File { get; set; }

    /// <summary>
    /// Information about the episode, such as titles
    /// </summary>
    public IReadOnlyList<IShokoEpisode> Episodes { get; set; }

    /// <summary>
    /// Information about the Anime, such as titles
    /// </summary>
    public IReadOnlyList<IShokoSeries> Series { get; set; }

    /// <summary>
    /// Information about the group
    /// </summary>
    public IReadOnlyList<IShokoGroup> Groups { get; set; }
}

/// <summary>
/// Event Args for File Relocation with Settings
/// </summary>
public class RelocationEventArgs<T> : RelocationEventArgs where T : class
{
    /// <summary>
    /// The settings for an <see cref="IRenamer{T}"/>
    /// </summary>
    public T Settings { get; set; }
}
