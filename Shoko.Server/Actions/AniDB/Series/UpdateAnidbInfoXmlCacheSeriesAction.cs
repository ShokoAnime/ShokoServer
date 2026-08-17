using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Metadata.Anidb.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Update AniDB info for the series using only locally cached XML data.
/// </summary>
public sealed class UpdateAnidbInfoXmlCacheSeriesAction(IAnidbService anidbService) : SeriesAction
{
    public override string Name => "Update AniDB Info - XML Cache";

    public override string? Description => "Updates AniDB data using information from local XML cache.";

    public override ActionCategory Category => ActionCategory.AniDB;

    public override ActionPermission Permission => ActionPermission.User;

    public override Task Execute(CancellationToken token = default)
        => anidbService.ScheduleRefreshOfAnimeByID(Series.AnidbAnimeID, AnidbRefreshMethod.Cache);
}
