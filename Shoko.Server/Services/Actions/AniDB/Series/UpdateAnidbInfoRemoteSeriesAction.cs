using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Action;
using Shoko.Abstractions.Action.Enums;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Metadata.Anidb.Services;
using Shoko.Abstractions.Metadata.Shoko;

namespace Shoko.Server.Services.Actions.AniDB.Series;

/// <summary>
///   Update AniDB info for the series using the remote API, respecting
///   the usual time and ban checks.
/// </summary>
public sealed class UpdateAnidbInfoRemoteSeriesAction(IAnidbService anidbService) : IExecutableSeriesSystemAction
{
    public string Name => "Update AniDB Info - Remote";
    public string? Description => "Gets the latest series information from the AniDB remote API, respecting usual checks.";
    public ActionCategory Category => ActionCategory.AniDB;

    public Task Execute(IShokoSeries series, CancellationToken cancellationToken = default)
        => anidbService.ScheduleRefreshOfAnimeByID(series.AnidbAnimeID, AnidbRefreshMethod.Remote);
}
