using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.ScheduledActions;
using Shoko.Abstractions.Utilities;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Scheduling.Jobs.Shoko;

namespace Shoko.Server.Actions;

/// <summary>
///   Queue the hashing of the files stored without a hash. A file is only
///   stored once hashed, so only rows from very old versions lack one.
/// </summary>
/// <remarks>
///   Only queues the hashing, which run on their own; the progress covers
///   the queuing.
/// </remarks>
/// <param name="scheduler">The queue.</param>
/// <param name="videoLocals">The files.</param>
public sealed class HashUnhashedFilesAction(IQueueScheduler scheduler, VideoLocalRepository videoLocals) : IScheduledAction
{
    public string Name => "Hash Unhashed Files";

    public string? Description => "Hash the files stored without a hash, which only very old versions left behind.";

    public ActionCategory Category => ActionCategory.Import;

    public async Task Execute(IProgress<decimal> progress, CancellationToken token)
    {
        var videos = videoLocals.GetVideosWithoutHash();
        var items = new ItemProgress(progress, videos.Count);
        items.Report(0);
        foreach (var video in videos)
        {
            token.ThrowIfCancellationRequested();
            if (video.FirstResolvedPlace?.Path is { } path)
                await scheduler.Enqueue<HashFileJob>(job => job.FilePath = path, ct: token);

            items.Increment();
        }
    }
}
