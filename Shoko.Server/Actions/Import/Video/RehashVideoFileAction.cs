using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.Shoko;

namespace Shoko.Server.Actions;

/// <summary>
///   Rehash the file.
/// </summary>
public sealed class RehashVideoFileAction(IQueueScheduler scheduler) : VideoAction
{
    public override string Name => "Rehash File";

    public override string? Description => "Rehashes the file.";

    public override ActionCategory Category => ActionCategory.Import;

    public override ActionPermission Permission => ActionPermission.Admin;

    public override Task<ActionValidationResult?> Validate(CancellationToken token = default)
        => Task.FromResult(string.IsNullOrEmpty(((VideoLocal)Video).FirstResolvedPlace?.Path)
            ? new ActionValidationResult("The file has no resolvable location on disk.")
            : null);

    public override async Task Execute(CancellationToken token = default)
    {
        var video = (VideoLocal)Video;
        var filePath = video.FirstResolvedPlace?.Path;
        if (string.IsNullOrEmpty(filePath))
            return;

        await scheduler.EnqueueWithPriority<HashFileJob>(
            c => (c.FilePath, c.ForceHash) = (filePath, true),
            JobPriorities.ForFileSize(video.FileSize, isNew: false, prioritize: true),
            ct: token
        );
    }
}
