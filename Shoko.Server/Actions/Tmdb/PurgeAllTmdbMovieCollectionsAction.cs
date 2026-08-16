using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Server.Providers.TMDB;

namespace Shoko.Server.Actions;

/// <summary>
///   Purge all TMDB movie collections from the local database.
/// </summary>
public sealed class PurgeAllTmdbMovieCollectionsAction(TmdbMetadataService tmdbService) : IExecutableAction
{
    public string Name => "Purge TMDB Movie Collections";

    public string? Description => "Remove all TMDB movie collections from the local database.";

    public ActionCategory Category => ActionCategory.TMDB;

    public ActionPermission Permission => ActionPermission.Admin;

    public bool RequiresConfirmation => true;

    public Task Execute(CancellationToken token = default)
        => tmdbService.PurgeAllMovieCollections();
}
