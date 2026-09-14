using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Server.Providers.Anilist;

namespace Shoko.Server.Actions;

/// <summary>
///   Automatically search for an AniList match for the series.
/// </summary>
public sealed class AutoSearchAnilistSeriesAction(AnilistMetadataService anilistService) : SeriesAction
{
    public override string Name => "Auto-Search AniList Match";

    public override string? Description => "Automatically searches for an AniList match.";

    public override ActionCategory Category => ActionCategory.AniList;

    public override ActionPermission Permission => ActionPermission.User;

    public override Task Execute(CancellationToken token = default)
        => anilistService.ScheduleSearchForMatch(Series.AnidbAnimeID, false);
}
