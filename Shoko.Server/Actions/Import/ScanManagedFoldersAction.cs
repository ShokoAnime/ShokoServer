using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.ScheduledActions;
using Shoko.Abstractions.Video.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Scan every file in the drop source folders, and the other managed
///   folders for new files, then import what changed.
/// </summary>
/// <param name="videoService">Schedules the scans.</param>
public sealed class ScanManagedFoldersAction(IVideoService videoService) : IScheduledAction
{
    public string Name => "Scan Managed Folders";

    public string? Description => "Scan every file in the drop source folders and the other managed folders for new files, hash them, and find releases.";

    public ActionCategory Category => ActionCategory.Import;

    public Task Execute(IProgress<decimal> progress, CancellationToken token)
        => videoService.ScheduleScanForManagedFolders();
}
