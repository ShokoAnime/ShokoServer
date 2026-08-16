using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Server.Providers.TMDB;

namespace Shoko.Server.Actions;

/// <summary>
///   Update all TMDB shows in the local database from the remote API,
///   including downloading any missing images.
/// </summary>
public sealed class UpdateAllTmdbShowsWithImagesAction(TmdbMetadataService tmdbService) : IExecutableAction
{
    public string Name => "Update All TMDB Shows (with Images)";

    public string? Description => "Update all TMDB show metadata and download any missing images.";

    public ActionCategory Category => ActionCategory.TMDB;

    // The legacy UpdateAllTmdbShows endpoint had no admin gate; keep User-level to avoid regressing callers.
    public ActionPermission Permission => ActionPermission.User;

    public Task Execute(CancellationToken token = default)
        => tmdbService.UpdateAllShows(true, true);
}
