using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.ScheduledActions;

namespace Shoko.Plugin.Tmdb.Actions;

/// <summary>
///   Purges every TMDb image that nothing links to any more.
/// </summary>
/// <param name="imageManager">The core's image manager, which removes them.</param>
public sealed class PurgeUnusedTmdbImagesAction(IImageManager imageManager) : IScheduledAction
{
    /// <inheritdoc/>
    public string Name => "Purge Unused TMDb Images";

    /// <inheritdoc/>
    public string? Description => "Remove all TMDb images that are not linked to any entity.";

    /// <inheritdoc/>
    public ActionCategory Category => ActionCategory.Images;

    /// <inheritdoc/>
    public bool RequiresConfirmation => true;

    /// <inheritdoc/>
    public string? ConfirmationMessage => "Are you sure you want to remove all unused TMDb images from the database?";

    /// <inheritdoc/>
    public Task Execute(IProgress<decimal> progress, CancellationToken token)
        => imageManager.PurgeOrphanedImages(0, MetadataSource.TMDB, progress, token);
}
