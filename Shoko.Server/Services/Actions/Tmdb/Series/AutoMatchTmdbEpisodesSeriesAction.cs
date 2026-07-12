using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Action;
using Shoko.Abstractions.Action.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Server.Providers.TMDB;

namespace Shoko.Server.Services.Actions.Tmdb.Series;

/// <summary>
///   Automatically match TMDB episodes for the series.
/// </summary>
internal sealed class AutoMatchTmdbEpisodesSeriesAction(TmdbLinkingService linkingService) : IExecutableSeriesAction
{
    public string Name => "Auto-Match TMDB Episodes";
    public string? Description => "Automatically matches Shoko episodes with TMDB episodes.";
    public ActionCategory Category => ActionCategory.TMDB;

    public Task Execute(IShokoSeries series, CancellationToken cancellationToken = default)
    {
        var tmdbShowId = series.TmdbShowCrossReferences is [{ } first, ..]
            ? first.TmdbShowID
            : 0;
        if (tmdbShowId is 0)
            return Task.CompletedTask;

        linkingService.MatchAnidbToTmdbEpisodes(series.AnidbAnimeID, tmdbShowId, null, useExisting: true, useExistingOtherShows: null, saveToDatabase: true);
        return Task.CompletedTask;
    }
}
