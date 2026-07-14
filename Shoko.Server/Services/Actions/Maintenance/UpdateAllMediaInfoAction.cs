using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Action;
using Shoko.Abstractions.Action.Enums;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Scheduling;
using Shoko.Server.Scheduling.Jobs.Actions;

namespace Shoko.Server.Services.Actions.Maintenance;

/// <summary>
///   Update media info for all files in the collection.
/// </summary>
internal sealed class UpdateAllMediaInfoAction(IQueueScheduler scheduler) : IExecutableGlobalSystemAction
{
    public string Name => "Update All Media Info";
    public string? Description => "Re-read and update media info for all files in the collection.";
    public ActionCategory Category => ActionCategory.Maintenance;

    public async Task Execute(CancellationToken cancellationToken = default)
    {
        await scheduler.StartJob<MediaInfoAllFilesJob>();
    }
}
