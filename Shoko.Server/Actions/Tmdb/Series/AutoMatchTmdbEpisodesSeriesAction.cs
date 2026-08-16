using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Server.Providers.TMDB;

namespace Shoko.Server.Actions;

/// <summary>
///   Automatically match TMDB episodes for the series.
/// </summary>
public sealed class AutoMatchTmdbEpisodesSeriesAction(TmdbLinkingService linkingService) : SeriesAction
{
    public override string Name => "Auto-Match TMDB Episodes";

    public string? Description => "Automatically matches Shoko episodes with TMDB episodes.";

    public ActionCategory Category => ActionCategory.TMDB;

    // The legacy TMDB/Show/CrossReferences/Episode/Auto endpoint was admin-gated; keep Admin-level so the permission surface does not widen.
    public override ActionPermission Permission => ActionPermission.Admin;

    public override Task Execute(CancellationToken token = default)
    {
        var tmdbShowId = Series.TmdbShowCrossReferences is [{ } first, ..]
            ? first.TmdbShowID
            : 0;
        if (tmdbShowId is 0)
            return Task.CompletedTask;

        linkingService.MatchAnidbToTmdbEpisodes(Series.AnidbAnimeID, tmdbShowId, null, useExisting: true, useExistingOtherShows: null, saveToDatabase: true);
        return Task.CompletedTask;
    }
}
