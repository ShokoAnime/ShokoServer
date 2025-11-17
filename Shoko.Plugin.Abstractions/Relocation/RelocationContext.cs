using System.Collections.Generic;
using System.Threading;
using Shoko.Plugin.Abstractions.Config;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.DataModels.Shoko;

namespace Shoko.Plugin.Abstractions.Relocation;

/// <summary>
/// The context for a relocation operation.
/// </summary>
public class RelocationContext(RelocationContext? context = null)
{
    /// <summary>
    /// The cancellation token for the relocation. If this is cancelled, the relocation will be cancelled.
    /// </summary>
    public CancellationToken CancellationToken { get; init; } = context?.CancellationToken ?? CancellationToken.None;

    /// <summary>
    /// If this operation can potentially move the file.
    /// </summary>
    public bool MoveEnabled { get; init; } = context?.MoveEnabled ?? true;

    /// <summary>
    /// If this operation can potentially rename the file.
    /// </summary>
    public bool RenameEnabled { get; init; } = context?.RenameEnabled ?? true;

    /// <summary>
    /// The available managed folders to choose as a destination.
    /// You can only set the <see cref="RelocationResult.ManagedFolder"/> to
    /// one of these. If a Folder has <see cref="DropFolderType.Excluded"/> set,
    /// then it won't be in this list, and even if you try to set it, it will be
    /// ignored.
    /// </summary>
    public IReadOnlyList<IManagedFolder> AvailableFolders { get; init; } = context?.AvailableFolders ?? [];

    /// <summary>
    /// The video file being moved. Has ties to the video and managed folder.
    /// </summary>
    public IVideoFile File { get; init; } = context?.File!;

    /// <summary>
    /// The video being moved. Provides access to the release info, media info,
    /// hashes, all files linked to the video, and so on, so forth.
    /// </summary>
    public IVideo Video { get; init; } = context?.Video!;

    /// <summary>
    /// Short-cut to all the episodes linked to the video being moved.
    /// </summary>
    public IReadOnlyList<IShokoEpisode> Episodes { get; init; } = context?.Episodes ?? [];

    /// <summary>
    /// Short-cut to all the series linked to the video being moved.
    /// </summary>
    public IReadOnlyList<IShokoSeries> Series { get; init; } = context?.Series ?? [];

    /// <summary>
    /// Short-cut to all the groups linked to the video being moved.
    /// </summary>
    public IReadOnlyList<IShokoGroup> Groups { get; init; } = context?.Groups ?? [];
}

/// <summary>
/// The context for a relocation operation with a configuration.
/// </summary>
public class RelocationContext<TConfig>(RelocationContext? context, TConfig configuration) : RelocationContext(context) where TConfig : IRelocationProviderConfiguration
{
    /// <summary>
    /// The configuration for the current renamer instance.
    /// </summary>
    public TConfig Configuration { get; set; } = configuration;
}
