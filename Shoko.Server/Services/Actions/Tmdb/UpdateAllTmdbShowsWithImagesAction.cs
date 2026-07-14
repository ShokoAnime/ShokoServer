using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Action;
using Shoko.Abstractions.Action.Enums;
using Shoko.Server.Providers.TMDB;

namespace Shoko.Server.Services.Actions.Tmdb;

/// <summary>
///   Update all TMDB shows in the local database from the remote API,
///   including downloading any missing images.
/// </summary>
internal sealed class UpdateAllTmdbShowsWithImagesAction(TmdbMetadataService tmdbService) : IExecutableGlobalSystemAction
{
    public string Name => "Update All TMDB Shows (with Images)";
    public string? Description => "Update all TMDB show metadata and download any missing images.";
    public ActionCategory Category => ActionCategory.TMDB;

    public async Task Execute(CancellationToken cancellationToken = default)
    {
        await tmdbService.UpdateAllShows(true, true);
    }
}
