using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.ScheduledActions;
using Shoko.Abstractions.Utilities;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Scheduling.Jobs.AniDB;
using Shoko.Server.Settings;

namespace Shoko.Server.Actions;

/// <summary>
///   Queue AVDump jobs for files whose media info and AniDB data are
///   mismatched (e.g., chapter states differ).
/// </summary>
/// <remarks>
///   Only queues the AVDump jobs, which run on their own; the progress covers
///   the queuing.
/// </remarks>
public sealed class AVDumpMismatchedFilesAction(
    IQueueScheduler scheduler,
    ISettingsProvider settingsProvider,
    VideoLocalRepository videoLocals,
    ILogger<AVDumpMismatchedFilesAction> logger
) : IScheduledAction
{
    public string Name => "AVDump Mismatched Files";

    public string? Description => "Queue AVDump jobs for files whose local media info and AniDB data are mismatched.";

    public ActionCategory Category => ActionCategory.AniDB;

    public Task<ActionValidationResult?> Validate(CancellationToken token)
        => Task.FromResult(string.IsNullOrWhiteSpace(settingsProvider.GetSettings().AniDb.AVDumpKey)
            ? new ActionValidationResult("Missing AVDump API key. Set it in the settings first.")
            : null);

    public async Task Execute(IProgress<decimal> progress, CancellationToken token)
    {
        var mismatchedFiles = videoLocals.GetAll()
            .Where(file => !file.IsEmpty() && file.MediaInfo != null)
            .Select(file => (Video: file, AniDB: file.ReleaseInfo))
            .Where(tuple => tuple.AniDB is { ProviderName: "AniDB", IsCorrupted: false } && tuple.Video.MediaInfo?.MenuStreams.Count != 0 != tuple.AniDB.IsChaptered)
            .Select(tuple => (Path: tuple.Video.FirstResolvedPlace?.Path, tuple.Video))
            .Where(tuple => !string.IsNullOrEmpty(tuple.Path))
            .ToDictionary(tuple => tuple.Video.VideoLocalID, tuple => tuple.Path!);
        var items = new ItemProgress(progress, mismatchedFiles.Count);
        items.Report(0);
        foreach (var (fileId, filePath) in mismatchedFiles)
        {
            token.ThrowIfCancellationRequested();
            await scheduler.Enqueue<AVDumpFilesJob>(a => a.Videos = new() { { fileId, filePath } }, ct: token);
            items.Increment();
        }

        logger.LogInformation("Queued {QueuedAnimeCount} files for avdumping", mismatchedFiles.Count);
    }
}
