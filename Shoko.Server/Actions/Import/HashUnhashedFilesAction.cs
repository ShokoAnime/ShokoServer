using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.ScheduledActions;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Scheduling.Jobs.Shoko;

namespace Shoko.Server.Actions;

/// <summary>
///   Queue the hashing of the files stored without a hash. A file is only
///   stored once hashed, so only rows from very old versions lack one.
/// </summary>
/// <param name="scheduler">The queue.</param>
/// <param name="videoLocals">The files.</param>
public sealed class HashUnhashedFilesAction(IQueueScheduler scheduler, VideoLocalRepository videoLocals) : IScheduledAction
{
    public string Name => "Hash Unhashed Files";

    public string? Description => "Hash the files stored without a hash, which only very old versions left behind.";

    public ActionCategory Category => ActionCategory.Import;

    public async Task Execute(IProgress<decimal> progress, CancellationToken token)
    {
        foreach (var video in videoLocals.GetVideosWithoutHash())
        {
            token.ThrowIfCancellationRequested();
            if (video.FirstResolvedPlace?.Path is not { } path)
                continue;

            await scheduler.Enqueue<HashFileJob>(job => job.FilePath = path, ct: token);
        }
    }
}
