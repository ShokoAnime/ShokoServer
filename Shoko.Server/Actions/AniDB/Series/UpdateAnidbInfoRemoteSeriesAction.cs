using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Metadata.Anidb.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Update AniDB info for the series using the remote API, respecting
///   the usual time and ban checks.
/// </summary>
public sealed class UpdateAnidbInfoRemoteSeriesAction(IAnidbService anidbService) : SeriesAction
{
    public override string Name => "Update AniDB Info - Remote";

    public override string? Description => "Gets the latest series information from the AniDB remote API, respecting usual checks.";

    public override ActionCategory Category => ActionCategory.AniDB;

    public override ActionPermission Permission => ActionPermission.User;

    public override Task Execute(CancellationToken token = default)
        => anidbService.ScheduleRefreshOfAnimeByID(Series.AnidbAnimeID, AnidbRefreshMethod.Remote);
}
