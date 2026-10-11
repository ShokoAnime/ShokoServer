using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Video.Events;
using Shoko.Abstractions.Video.Services;

namespace Shoko.Server.Services;

/// <summary>
///   Queues the image contributors for each video that is new to the
///   database, unless the hashing asked for no follow-up work.
/// </summary>
/// <param name="videoService">Raises the event for a hashed video.</param>
/// <param name="contributorScheduler">Queues the contributors.</param>
/// <param name="logger">Where failures to queue are reported.</param>
public class VideoImageContributorListener(
    IVideoService videoService,
    MetadataImageContributorScheduler contributorScheduler,
    ILogger<VideoImageContributorListener> logger
) : IHostedService
{
    #region Lifetime

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        videoService.VideoFileHashed += OnVideoFileHashed;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        videoService.VideoFileHashed -= OnVideoFileHashed;
        return Task.CompletedTask;
    }

    #endregion

    #region Events

    private async void OnVideoFileHashed(object? sender, VideoFileHashedEventArgs e)
    {
        if (e.SkipEvents || !e.IsNewVideo)
            return;

        try
        {
            await contributorScheduler.ScheduleForEntry(e.Video.ID).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to queue the image contributors for the new video {VideoID}.", e.Video.ID);
        }
    }

    #endregion
}
