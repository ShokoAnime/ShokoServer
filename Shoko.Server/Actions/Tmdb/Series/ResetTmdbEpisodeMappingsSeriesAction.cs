using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Server.Providers.TMDB;

namespace Shoko.Server.Actions;

/// <summary>
///   Reset all TMDB episode mappings for the series.
/// </summary>
public sealed class ResetTmdbEpisodeMappingsSeriesAction(TmdbLinkingService linkingService) : SeriesAction
{
    public override string Name => "Reset TMDB Episode Mappings";

    public string? Description => "Reset all TMDB episode mappings for the series.";

    public ActionCategory Category => ActionCategory.TMDB;

    // The legacy TMDB/Show/CrossReferences/Episode endpoint was admin-gated; keep Admin-level so the permission surface does not widen.
    public override ActionPermission Permission => ActionPermission.Admin;

    public bool RequiresConfirmation => true;

    public override Task Execute(CancellationToken token = default)
    {
        linkingService.ResetAllEpisodeLinks(Series.AnidbAnimeID, true);
        return Task.CompletedTask;
    }
}
