using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Server.Providers.TMDB;

namespace Shoko.Server.Actions;

/// <summary>
///   Purge all TMDB shows that are not linked to any AniDB anime.
/// </summary>
public sealed class PurgeAllUnusedTmdbShowsAction(TmdbMetadataService tmdbService) : IExecutableAction
{
    public string Name => "Purge Unused TMDB Shows";

    public string? Description => "Remove all TMDB shows that are not linked to any AniDB anime.";

    public ActionCategory Category => ActionCategory.TMDB;

    public ActionPermission Permission => ActionPermission.Admin;

    public bool RequiresConfirmation => true;

    public string? ConfirmationMessage => "Are you sure you want to remove all unused TMDB shows from the database?";

    public Task Execute(CancellationToken token = default)
        => tmdbService.PurgeAllUnusedShows();
}
