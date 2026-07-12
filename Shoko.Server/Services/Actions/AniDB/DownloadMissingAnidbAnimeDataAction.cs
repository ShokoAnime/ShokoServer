using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Action;
using Shoko.Abstractions.Action.Enums;

namespace Shoko.Server.Services.Actions.AniDB;

/// <summary>
///   Download missing AniDB XML data for anime, and fix cross-references with
///   incomplete data.
/// </summary>
internal sealed class DownloadMissingAnidbAnimeDataAction(ActionService actionService) : IExecutableGlobalAction
{
    public string Name => "Download Missing AniDB Anime Data";
    public string? Description => "Download missing AniDB XML data and fix cross-references with incomplete data.";
    public ActionCategory Category => ActionCategory.AniDB;

    public async Task Execute(CancellationToken cancellationToken = default)
    {
        await actionService.DownloadMissingAnidbAnimeXmls();
        await actionService.ScheduleMissingAnidbAnimeForFiles();
    }
}
