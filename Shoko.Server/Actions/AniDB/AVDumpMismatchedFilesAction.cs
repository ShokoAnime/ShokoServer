using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Actions;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Scheduling.Jobs.AniDB;
using Shoko.Server.Settings;

namespace Shoko.Server.Actions;

/// <summary>
///   Queue AVDump jobs for files whose media info and AniDB data are
///   mismatched (e.g., chapter states differ).
/// </summary>
public sealed class AVDumpMismatchedFilesAction(
    IQueueScheduler scheduler,
    ISettingsProvider settingsProvider,
    VideoLocalRepository videoLocals,
    ILogger<AVDumpMismatchedFilesAction> logger
) : IExecutableAction
{
    public string Name => "AVDump Mismatched Files";

    public string? Description => "Queue AVDump jobs for files whose local media info and AniDB data are mismatched.";

    public ActionCategory Category => ActionCategory.AniDB;

    public ActionPermission Permission => ActionPermission.Admin;

    public Task<ActionValidationResult?> Validate(CancellationToken token = default)
        => Task.FromResult(string.IsNullOrWhiteSpace(settingsProvider.GetSettings().AniDb.AVDumpKey)
            ? new ActionValidationResult("Missing AVDump API key. Set it in the settings first.")
            : null);

    public async Task Execute(CancellationToken token = default)
    {
        var mismatchedFiles = videoLocals.GetAll()
            .Where(file => !file.IsEmpty() && file.MediaInfo != null)
            .Select(file => (Video: file, AniDB: file.ReleaseInfo))
            .Where(tuple => tuple.AniDB is { ProviderName: "AniDB", IsCorrupted: false } && tuple.Video.MediaInfo?.MenuStreams.Count != 0 != tuple.AniDB.IsChaptered)
            .Select(tuple => (Path: tuple.Video.FirstResolvedPlace?.Path, tuple.Video))
            .Where(tuple => !string.IsNullOrEmpty(tuple.Path))
            .ToDictionary(tuple => tuple.Video.VideoLocalID, tuple => tuple.Path);
        foreach (var (fileId, filePath) in mismatchedFiles)
            await scheduler.Enqueue<AVDumpFilesJob>(a => a.Videos = new() { { fileId, filePath } }, ct: token);

        logger.LogInformation("Queued {QueuedAnimeCount} files for avdumping", mismatchedFiles.Count);
    }
}
