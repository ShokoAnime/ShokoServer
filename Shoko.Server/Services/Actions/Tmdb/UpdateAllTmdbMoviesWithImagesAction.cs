using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Action;
using Shoko.Abstractions.Action.Enums;
using Shoko.Server.Providers.TMDB;

namespace Shoko.Server.Services.Actions.Tmdb;

/// <summary>
///   Update all TMDB movies in the local database from the remote API,
///   including downloading any missing images.
/// </summary>
public sealed class UpdateAllTmdbMoviesWithImagesAction(TmdbMetadataService tmdbService) : IExecutableGlobalSystemAction
{
    public string Name => "Update All TMDB Movies (with Images)";
    public string? Description => "Update all TMDB movie metadata and download any missing images.";
    public ActionCategory Category => ActionCategory.TMDB;

    public async Task Execute(CancellationToken cancellationToken = default)
    {
        await tmdbService.UpdateAllMovies(true, true);
    }
}
