using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.ScheduledActions;
using Shoko.Abstractions.Utilities;
using Shoko.Abstractions.Video.Services;
using Shoko.Server.Repositories.Cached;

namespace Shoko.Server.Actions;

/// <summary>
///   Ignore again the files with the hash of a file that is ignored.
/// </summary>
/// <param name="videoLocals">The files.</param>
/// <param name="videoService">Marks the files as ignored.</param>
public sealed class CheckForPreviouslyIgnoredFilesAction(VideoLocalRepository videoLocals, IVideoService videoService) : IScheduledAction
{
    public string Name => "Check for Previously Ignored Files";

    public string? Description => "Ignore the files that share their hash with a file that is ignored.";

    public ActionCategory Category => ActionCategory.Import;

    public Task Execute(IProgress<decimal> progress, CancellationToken token)
    {
        var ignoredHashes = videoLocals.GetIgnoredVideos()
            .Select(video => video.Hash)
            .ToHashSet();
        var videos = videoLocals.GetAll();
        var items = new ItemProgress(progress, videos.Count);
        items.Report(0);
        foreach (var video in videos)
        {
            token.ThrowIfCancellationRequested();
            if (!video.IsIgnored && ignoredHashes.Contains(video.Hash))
                videoService.SetVideoIgnored(video, true);

            items.Increment();
        }

        return Task.CompletedTask;
    }
}
