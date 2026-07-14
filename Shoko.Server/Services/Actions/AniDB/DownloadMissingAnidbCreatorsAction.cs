using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Action;
using Shoko.Abstractions.Action.Enums;

namespace Shoko.Server.Services.Actions.AniDB;

/// <summary>
///   Download all missing AniDB creator data via the UDP API.
/// </summary>
internal sealed class DownloadMissingAnidbCreatorsAction(ActionService actionService) : IExecutableGlobalSystemAction
{
    public string Name => "Download Missing AniDB Creators";
    public string? Description => "Download all missing or incomplete AniDB creator data via the UDP API.";
    public ActionCategory Category => ActionCategory.AniDB;

    public async Task Execute(CancellationToken cancellationToken = default)
    {
        await actionService.ScheduleMissingAnidbCreators();
    }
}
