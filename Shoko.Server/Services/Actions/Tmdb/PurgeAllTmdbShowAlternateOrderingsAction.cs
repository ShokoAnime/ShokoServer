using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Action;
using Shoko.Abstractions.Action.Enums;
using Shoko.Server.Providers.TMDB;

namespace Shoko.Server.Services.Actions.Tmdb;

/// <summary>
///   Purge all TMDB show alternate orderings from the local database.
/// </summary>
public sealed class PurgeAllTmdbShowAlternateOrderingsAction(TmdbMetadataService tmdbService) : IExecutableGlobalSystemAction
{
    public string Name => "Purge TMDB Show Alternate Orderings";
    public string? Description => "Remove all TMDB show alternate orderings (episode groups) from the local database.";
    public ActionCategory Category => ActionCategory.TMDB;
    public bool RequiresConfirmation => true;

    public Task Execute(CancellationToken cancellationToken = default)
    {
        tmdbService.PurgeAllShowEpisodeGroups();
        return Task.CompletedTask;
    }
}
