using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.ScheduledActions;

namespace Shoko.Server.Actions;

/// <summary>
///   Schedule auto-downloads for all missing images across all entities.
/// </summary>
/// <remarks>
///   Only queues the downloads, which run on their own; the progress covers
///   the queuing.
/// </remarks>
public sealed class UpdateAllImagesAction(IImageManager imageManager) : IScheduledAction
{
    public string Name => "Update All Images";

    public string? Description => "Schedule downloads for all missing images across all entities.";

    public ActionCategory Category => ActionCategory.Images;

    public Task Execute(IProgress<decimal> progress, CancellationToken token)
        => imageManager.ScheduleAllAutoDownloads(null, null, null, false, progress, token);
}
