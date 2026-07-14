using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Action;
using Shoko.Abstractions.Action.Enums;
using Shoko.Server.Providers.TMDB;

namespace Shoko.Server.Services.Actions.Tmdb;

/// <summary>
///   Purge all TMDB movies that are not linked to any AniDB anime.
/// </summary>
public sealed class PurgeAllUnusedTmdbMoviesAction(TmdbMetadataService tmdbService) : IExecutableGlobalSystemAction
{
    public string Name => "Purge Unused TMDB Movies";
    public string? Description => "Remove all TMDB movies that are not linked to any AniDB anime.";
    public ActionCategory Category => ActionCategory.TMDB;
    public bool RequiresConfirmation => true;

    public async Task Execute(CancellationToken cancellationToken = default)
    {
        await tmdbService.PurgeAllUnusedMovies();
    }
}
