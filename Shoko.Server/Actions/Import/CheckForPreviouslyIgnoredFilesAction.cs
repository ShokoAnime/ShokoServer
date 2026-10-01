using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.ScheduledActions;
using Shoko.Server.Repositories.Cached;

namespace Shoko.Server.Actions;

/// <summary>
///   Ignore again the files with the hash of a file that is ignored.
/// </summary>
/// <param name="videoLocals">The files.</param>
public sealed class CheckForPreviouslyIgnoredFilesAction(VideoLocalRepository videoLocals) : IScheduledAction
{
    public string Name => "Check for Previously Ignored Files";

    public string? Description => "Ignore the files that share their hash with a file that is ignored.";

    public ActionCategory Category => ActionCategory.Import;

    public Task Execute(IProgress<decimal> progress, CancellationToken token)
    {
        var ignoredHashes = videoLocals.GetIgnoredVideos()
            .Select(video => video.Hash)
            .ToHashSet();
        foreach (var video in videoLocals.GetAll())
        {
            token.ThrowIfCancellationRequested();
            if (video.IsIgnored || !ignoredHashes.Contains(video.Hash))
                continue;

            video.IsIgnored = true;
            videoLocals.Save(video, false);
        }

        return Task.CompletedTask;
    }
}
