using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Metadata.Anidb.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Update AniDB info for the series, using cached data if available and
///   falling back to the remote API.
/// </summary>
public sealed class UpdateAnidbInfoSeriesAction(IAnidbService anidbService) : SeriesAction
{
    public override string Name => "Update AniDB Info";

    public string? Description => "Gets the latest series information from the AniDB database.";

    public ActionCategory Category => ActionCategory.AniDB;

    public override ActionPermission Permission => ActionPermission.User;

    public override Task Execute(CancellationToken token = default)
        => anidbService.ScheduleRefreshOfAnimeByID(Series.AnidbAnimeID, AnidbRefreshMethod.Cache | AnidbRefreshMethod.DeferToRemoteIfUnsuccessful);
}
