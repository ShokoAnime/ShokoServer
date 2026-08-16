using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Server.Providers.TMDB;

namespace Shoko.Server.Actions;

/// <summary>
///   Automatically search for a TMDB match for the series.
/// </summary>
public sealed class AutoSearchTmdbSeriesAction(TmdbMetadataService tmdbService) : SeriesAction
{
    public override string Name => "Auto-Search TMDB Match";

    public string? Description => "Automatically searches for a TMDB match.";

    public ActionCategory Category => ActionCategory.TMDB;

    public override ActionPermission Permission => ActionPermission.User;

    public override Task Execute(CancellationToken token = default)
        => tmdbService.ScheduleSearchForMatch(Series.AnidbAnimeID, false);
}
