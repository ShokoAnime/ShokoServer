using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Scheduling.Jobs.AniDB;
using Shoko.Server.Settings;

namespace Shoko.Server.Actions;

/// <summary>
///   Run the file through AVDump and report the result to AniDB.
/// </summary>
/// <remarks>
///   The endpoint this mirrors, <c>POST /api/v3/File/{fileID}/AVDump</c>, can
///   run the dump inline and hand the result back. An action cannot: it is
///   always queued, and there is nowhere to return a result to. This takes the
///   queued path only, which is that endpoint's <c>immediate: false</c> branch.
/// </remarks>
public sealed class AVDumpVideoFileAction(IQueueScheduler scheduler, ISettingsProvider settingsProvider) : VideoAction
{
    public override string Name => "AVDump File";

    public override string? Description => "Runs the file through AVDump and reports the result to AniDB.";

    public override ActionCategory Category => ActionCategory.AniDB;

    public override ActionPermission Permission => ActionPermission.Admin;

    public override Task<ActionValidationResult?> Validate(CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(settingsProvider.GetSettings().AniDb.AVDumpKey))
            return Task.FromResult<ActionValidationResult?>(new("Missing AVDump API key. Set it in the settings first."));

        if (string.IsNullOrEmpty(((VideoLocal)Video).FirstResolvedPlace?.Path))
            return Task.FromResult<ActionValidationResult?>(new("The file has no resolvable location on disk."));

        return Task.FromResult<ActionValidationResult?>(null);
    }

    public override async Task Execute(CancellationToken token = default)
    {
        var video = (VideoLocal)Video;
        if (video.FirstResolvedPlace?.Path is not { Length: > 0 } filePath)
            return;

        await scheduler.Enqueue<AVDumpFilesJob>(a => a.Videos = new Dictionary<int, string> { { video.VideoLocalID, filePath } }, ct: token);
    }
}
