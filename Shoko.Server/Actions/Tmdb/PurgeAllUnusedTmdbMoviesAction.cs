using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Server.Providers.TMDB;

namespace Shoko.Server.Actions;

/// <summary>
///   Purge all TMDB movies that are not linked to any AniDB anime.
/// </summary>
public sealed class PurgeAllUnusedTmdbMoviesAction(TmdbMetadataService tmdbService) : IExecutableAction
{
    public string Name => "Purge Unused TMDB Movies";

    public string? Description => "Remove all TMDB movies that are not linked to any AniDB anime.";

    public ActionCategory Category => ActionCategory.TMDB;

    public ActionPermission Permission => ActionPermission.Admin;

    public bool RequiresConfirmation => true;

    public Task Execute(CancellationToken token = default)
        => tmdbService.PurgeAllUnusedMovies();
}
