using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Scheduling.Jobs.Actions;

namespace Shoko.Server.Actions;

/// <summary>
///   Update media info for all files in the collection.
/// </summary>
public sealed class UpdateAllMediaInfoAction(IQueueScheduler scheduler) : IExecutableAction
{
    public string Name => "Update All Media Info";

    public string? Description => "Re-read and update media info for all files in the collection.";

    public ActionCategory Category => ActionCategory.Maintenance;

    public ActionPermission Permission => ActionPermission.Admin;

    public Task Execute(CancellationToken token = default)
        => scheduler.Enqueue<MediaInfoAllFilesJob>(ct: token);
}
