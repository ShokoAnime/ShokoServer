using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Scheduling.Jobs.AniDB;

namespace Shoko.Server.Actions;

/// <summary>
///   Forcibly runs AddToMyList commands for all manually linked files.
/// </summary>
public sealed class AddAllManualLinksToMyListAction(IQueueScheduler scheduler, VideoLocalRepository videoLocals) : IExecutableAction
{
    public string Name => "Add All Manual Links to MyList";

    public string? Description => "Forcibly run AddToMyList commands for all files with manual links.";

    public ActionCategory Category => ActionCategory.AniDB;

    public ActionPermission Permission => ActionPermission.Admin;

    public async Task Execute(CancellationToken token = default)
    {
        var files = videoLocals.GetManuallyLinkedVideos();
        foreach (var vl in files)
            await scheduler.Enqueue<AddFileToMyListJob>(c => c.Hash = vl.Hash, ct: token);
    }
}
