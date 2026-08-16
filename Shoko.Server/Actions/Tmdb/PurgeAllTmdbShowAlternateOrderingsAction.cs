using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Server.Providers.TMDB;

namespace Shoko.Server.Actions;

/// <summary>
///   Purge all TMDB show alternate orderings from the local database.
/// </summary>
public sealed class PurgeAllTmdbShowAlternateOrderingsAction(TmdbMetadataService tmdbService) : IExecutableAction
{
    public string Name => "Purge TMDB Show Alternate Orderings";

    public string? Description => "Remove all TMDB show alternate orderings (episode groups) from the local database.";

    public ActionCategory Category => ActionCategory.TMDB;

    // The legacy PurgeAllTmdbShowAlternateOrderings endpoint was admin-gated; keep Admin-level so the permission surface does not widen.
    public ActionPermission Permission => ActionPermission.Admin;

    public bool RequiresConfirmation => true;

    public Task Execute(CancellationToken token = default)
    {
        tmdbService.PurgeAllShowEpisodeGroups();
        return Task.CompletedTask;
    }
}
