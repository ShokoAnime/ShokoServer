using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Action;
using Shoko.Abstractions.Action.Enums;
using Shoko.Server.Providers.TMDB;

namespace Shoko.Server.Services.Actions.Tmdb;

/// <summary>
///   Purge all TMDB shows that are not linked to any AniDB anime.
/// </summary>
internal sealed class PurgeAllUnusedTmdbShowsAction(TmdbMetadataService tmdbService) : IExecutableGlobalAction
{
    public string Name => "Purge Unused TMDB Shows";
    public string? Description => "Remove all TMDB shows that are not linked to any AniDB anime.";
    public ActionCategory Category => ActionCategory.TMDB;
    public bool RequiresConfirmation => true;

    public async Task Execute(CancellationToken cancellationToken = default)
    {
        await tmdbService.PurgeAllUnusedShows();
    }
}
