using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Server.Providers.TMDB;

namespace Shoko.Server.Actions;

/// <summary>
///   Update all TMDB shows in the local database from the remote API.
///   Only refreshes metadata; does not download images.
/// </summary>
public sealed class UpdateAllTmdbShowsAction(TmdbMetadataService tmdbService) : IExecutableAction
{
    public string Name => "Update All TMDB Shows";

    public string? Description => "Update all TMDB show metadata in the local database without downloading images.";

    public ActionCategory Category => ActionCategory.TMDB;

    // Matches today: no [Authorize("admin")] on the legacy UpdateAllTmdbShows endpoint.
    public ActionPermission Permission => ActionPermission.User;

    public Task Execute(CancellationToken token = default)
        => tmdbService.UpdateAllShows(true, false);
}
