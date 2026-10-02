using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.ScheduledActions;
using Shoko.Server.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Update all TMDB shows in the local database from the remote API.
///   Only refreshes metadata; does not download images.
/// </summary>
/// <remarks>
///   Only queues the refreshes, which run on their own; the progress covers
///   the queuing.
/// </remarks>
public sealed class UpdateAllTmdbShowsAction(IMetadataRefreshService refreshService) : IScheduledAction
{
    public string Name => "Update All TMDB Shows";

    public string? Description => "Update all TMDB show metadata in the local database without downloading images.";

    public ActionCategory Category => ActionCategory.TMDB;

    public Task Execute(IProgress<decimal> progress, CancellationToken token)
        => refreshService.RefreshAllLinked(
            MetadataSource.TMDB,
            force: true,
            new() { DownloadImages = false, Reason = MetadataRefreshReason.Requested },
            MetadataEntityType.Series,
            progress,
            token
        );
}
