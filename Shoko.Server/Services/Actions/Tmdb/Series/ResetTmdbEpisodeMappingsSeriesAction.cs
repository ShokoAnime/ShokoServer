using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Action;
using Shoko.Abstractions.Action.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Server.Providers.TMDB;

namespace Shoko.Server.Services.Actions.Tmdb.Series;

/// <summary>
///   Reset all TMDB episode mappings for the series.
/// </summary>
public sealed class ResetTmdbEpisodeMappingsSeriesAction(TmdbLinkingService linkingService) : IExecutableSeriesSystemAction
{
    public string Name => "Reset TMDB Episode Mappings";
    public string? Description => "Reset all TMDB episode mappings for the series.";
    public ActionCategory Category => ActionCategory.TMDB;
    public bool RequiresConfirmation => true;

    public Task Execute(IShokoSeries series, CancellationToken cancellationToken = default)
    {
        linkingService.ResetAllEpisodeLinks(series.AnidbAnimeID, true);
        return Task.CompletedTask;
    }
}
