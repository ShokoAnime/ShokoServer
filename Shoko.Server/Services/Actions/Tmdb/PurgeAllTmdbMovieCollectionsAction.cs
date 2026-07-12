using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Action;
using Shoko.Abstractions.Action.Enums;
using Shoko.Server.Providers.TMDB;

namespace Shoko.Server.Services.Actions.Tmdb;

/// <summary>
///   Purge all TMDB movie collections from the local database.
/// </summary>
internal sealed class PurgeAllTmdbMovieCollectionsAction(TmdbMetadataService tmdbService) : IExecutableGlobalAction
{
    public string Name => "Purge TMDB Movie Collections";
    public string? Description => "Remove all TMDB movie collections from the local database.";
    public ActionCategory Category => ActionCategory.TMDB;
    public bool RequiresConfirmation => true;

    public async Task Execute(CancellationToken cancellationToken = default)
    {
        await tmdbService.PurgeAllMovieCollections();
    }
}
