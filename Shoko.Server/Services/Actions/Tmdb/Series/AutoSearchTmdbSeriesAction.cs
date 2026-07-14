using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Action;
using Shoko.Abstractions.Action.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Server.Providers.TMDB;

namespace Shoko.Server.Services.Actions.Tmdb.Series;

/// <summary>
///   Automatically search for a TMDB match for the series.
/// </summary>
internal sealed class AutoSearchTmdbSeriesAction(TmdbMetadataService tmdbService) : IExecutableSeriesSystemAction
{
    public string Name => "Auto-Search TMDB Match";
    public string? Description => "Automatically searches for a TMDB match.";
    public ActionCategory Category => ActionCategory.TMDB;

    public Task Execute(IShokoSeries series, CancellationToken cancellationToken = default)
        => tmdbService.ScheduleSearchForMatch(series.AnidbAnimeID, false);
}
