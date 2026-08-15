using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Server.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Download missing AniDB XML data for anime, and fix cross-references with
///   incomplete data.
/// </summary>
public sealed class DownloadMissingAnidbAnimeDataAction(ActionService actionService) : IExecutableAction
{
    public string Name => "Download Missing AniDB Anime Data";

    public string? Description => "Download missing AniDB XML data and fix cross-references with incomplete data.";

    public ActionCategory Category => ActionCategory.AniDB;

    // Matches today: [Authorize("admin")] on the legacy DownloadMissingAniDBAnimeData endpoint.
    public ActionPermission Permission => ActionPermission.Admin;

    public async Task Execute(CancellationToken token = default)
    {
        await actionService.DownloadMissingAnidbAnimeXmls();
        await actionService.ScheduleMissingAnidbAnimeForFiles();
    }
}
