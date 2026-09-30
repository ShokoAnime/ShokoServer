using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.ScheduledActions;

namespace Shoko.Server.Actions;

/// <summary>
///   Update all TMDB movies in the local database from the remote API,
///   including downloading any missing images.
/// </summary>
public sealed class UpdateAllTmdbMoviesWithImagesAction(IMetadataRefreshService refreshService) : IScheduledAction
{
    public string Name => "Update All TMDB Movies (with Images)";

    public string? Description => "Update all TMDB movie metadata and download any missing images.";

    public ActionCategory Category => ActionCategory.TMDB;

    public Task Execute(IProgress<decimal> progress, CancellationToken token)
        => refreshService.RefreshAllLinked(
            MetadataSource.TMDB,
            force: true,
            new() { DownloadImages = true, Reason = MetadataRefreshReason.Requested },
            MetadataEntityType.Movie,
            token
        );
}
