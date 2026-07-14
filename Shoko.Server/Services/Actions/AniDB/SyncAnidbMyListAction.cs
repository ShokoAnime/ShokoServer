using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Action;
using Shoko.Abstractions.Action.Enums;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Scheduling.Jobs.AniDB;

namespace Shoko.Server.Services.Actions.AniDB;

/// <summary>
///   Sync all local state to the AniDB MyList. This overwrites AniDB data.
/// </summary>
public sealed class SyncAnidbMyListAction(IJobFactory jobFactory) : IExecutableGlobalSystemAction
{
    public string Name => "Sync AniDB MyList";
    public string? Description => "Sync all local state to the AniDB MyList. This can overwrite AniDB data irreversibly.";
    public ActionCategory Category => ActionCategory.AniDB;
    public bool RequiresConfirmation => true;

    public async Task Execute(CancellationToken cancellationToken = default)
    {
        await jobFactory.Execute<SyncAniDBMyListJob>(j => j.ForceRefresh = true);
    }
}
