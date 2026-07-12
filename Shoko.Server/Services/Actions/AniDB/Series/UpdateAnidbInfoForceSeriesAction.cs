using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Action;
using Shoko.Abstractions.Action.Enums;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Metadata.Anidb.Services;
using Shoko.Abstractions.Metadata.Shoko;

namespace Shoko.Server.Services.Actions.AniDB.Series;

/// <summary>
///   Force a complete update of AniDB info for the series, bypassing time
///   checks and HTTP bans. Requires user confirmation.
/// </summary>
internal sealed class UpdateAnidbInfoForceSeriesAction(IAnidbService anidbService) : IExecutableSeriesAction
{
    public string Name => "Update AniDB Info - Force";
    public string? Description => "Forces a complete update from AniDB, bypassing usual checks and bans.";
    public ActionCategory Category => ActionCategory.AniDB;

    public bool RequiresConfirmation => true;

    public Task Execute(IShokoSeries series, CancellationToken cancellationToken = default)
        => anidbService.ScheduleRefreshOfAnimeByID(series.AnidbAnimeID, AnidbRefreshMethod.Remote | AnidbRefreshMethod.IgnoreTimeCheck | AnidbRefreshMethod.IgnoreHttpBans);
}
