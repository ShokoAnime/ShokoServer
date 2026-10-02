using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.ScheduledActions;

namespace Shoko.Server.Actions;

/// <summary>
///   Schedule auto-downloads for all images across all entities, of every
///   source and type, skipping the images already downloaded.
/// </summary>
/// <remarks>
///   Only queues the downloads, which run on their own; the progress covers
///   the queuing.
/// </remarks>
public sealed class DownloadAllImagesAction(IImageManager imageManager) : IScheduledAction
{
    public string Name => "Download All Images";

    public string? Description => "Schedule downloads for all images across all entities.";

    public ActionCategory Category => ActionCategory.Images;

    public Task Execute(IProgress<decimal> progress, CancellationToken token)
        => imageManager.ScheduleAllAutoDownloads(null, null, null, false, progress, token);
}
