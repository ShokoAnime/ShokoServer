using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.ScheduledActions;
using Shoko.Abstractions.Video.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Scan managed folders for new files and import them without running the
///   full metadata/image pipeline.
/// </summary>
/// <remarks>
///   Only queues the folder scans, which run on their own; the progress covers
///   the queuing.
/// </remarks>
public sealed class ImportNewFilesAction(IVideoService videoService) : IScheduledAction
{
    public string Name => "Import New Files";

    public string? Description => "Scan managed folders for new files, hash them, and find releases.";

    public ActionCategory Category => ActionCategory.Import;

    public Task Execute(IProgress<decimal> progress, CancellationToken token)
        => videoService.ScheduleScanForManagedFolders(false, true, false, null, false, true, progress, token);
}
