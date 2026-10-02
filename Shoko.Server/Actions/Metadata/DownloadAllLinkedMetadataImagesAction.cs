using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.ScheduledActions;
using Shoko.Abstractions.Utilities;
using Shoko.Server.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Link and download again the images of everything linked from the plugin
///   sources, for every anime.
/// </summary>
/// <remarks>
///   TMDB has image actions of its own.
///   Only queues the image jobs, which run on their own; the progress covers
///   the queuing.
/// </remarks>
public sealed class DownloadAllLinkedMetadataImagesAction(MetadataProviderScheduler providerScheduler, IMetadataRefreshService refreshService) : IScheduledAction
{
    public string Name => "Download All Linked Metadata Images - Force";

    public string? Description => "Links and downloads again the images of everything linked from the plugin metadata providers, for every anime.";

    public ActionCategory Category => ActionCategory.Images;

    public async Task Execute(IProgress<decimal> progress, CancellationToken token)
    {
        var sources = providerScheduler.GetImageProviders().Select(info => info.Source).Distinct().ToList();
        var stages = new StagedProgress(progress, Math.Max(sources.Count, 1));
        foreach (var source in sources)
        {
            await refreshService.DownloadAllImages(source, true, stages, token).ConfigureAwait(false);
            stages.NextStage();
        }
    }
}
