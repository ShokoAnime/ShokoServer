using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Action;
using Shoko.Abstractions.Action.Enums;
using Shoko.Server.Providers.TMDB;

namespace Shoko.Server.Services.Actions.Tmdb;

/// <summary>
///   Update all TMDB movies in the local database from the remote API.
///   Only refreshes metadata; does not download images.
/// </summary>
internal sealed class UpdateAllTmdbMoviesAction(TmdbMetadataService tmdbService) : IExecutableGlobalAction
{
    public string Name => "Update All TMDB Movies";
    public string? Description => "Update all TMDB movie metadata in the local database without downloading images.";
    public ActionCategory Category => ActionCategory.TMDB;

    public async Task Execute(CancellationToken cancellationToken = default)
    {
        await tmdbService.UpdateAllMovies(true, false);
    }
}
