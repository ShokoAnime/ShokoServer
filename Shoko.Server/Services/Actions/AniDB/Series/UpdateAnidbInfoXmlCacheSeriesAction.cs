using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Action;
using Shoko.Abstractions.Action.Enums;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Metadata.Anidb.Services;
using Shoko.Abstractions.Metadata.Shoko;

namespace Shoko.Server.Services.Actions.AniDB.Series;

/// <summary>
///   Update AniDB info for the series using only locally cached XML data.
/// </summary>
internal sealed class UpdateAnidbInfoXmlCacheSeriesAction(IAnidbService anidbService) : IExecutableSeriesSystemAction
{
    public string Name => "Update AniDB Info - XML Cache";
    public string? Description => "Updates AniDB data using information from local XML cache.";
    public ActionCategory Category => ActionCategory.AniDB;

    public Task Execute(IShokoSeries series, CancellationToken cancellationToken = default)
        => anidbService.ScheduleRefreshOfAnimeByID(series.AnidbAnimeID, AnidbRefreshMethod.Cache);
}
