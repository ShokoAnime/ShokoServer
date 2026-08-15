using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Metadata.Anidb.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Force a complete update of AniDB info for the series, bypassing time
///   checks and HTTP bans. Requires user confirmation.
/// </summary>
public sealed class UpdateAnidbInfoForceSeriesAction(IAnidbService anidbService) : SeriesAction
{
    public override string Name => "Update AniDB Info - Force";

    public string? Description => "Forces a complete update from AniDB, bypassing usual checks and bans.";

    public ActionCategory Category => ActionCategory.AniDB;

    public override ActionPermission Permission => ActionPermission.User;

    public bool RequiresConfirmation => true;

    public override Task Execute(CancellationToken token = default)
        => anidbService.ScheduleRefreshOfAnimeByID(Series.AnidbAnimeID, AnidbRefreshMethod.Remote | AnidbRefreshMethod.IgnoreTimeCheck | AnidbRefreshMethod.IgnoreHttpBans);
}
