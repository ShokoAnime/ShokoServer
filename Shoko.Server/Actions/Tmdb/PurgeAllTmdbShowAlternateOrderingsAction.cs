using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.ScheduledActions;
using Shoko.Server.Providers.TMDB;

namespace Shoko.Server.Actions;

/// <summary>
///   Purge all TMDB show alternate orderings from the local database.
/// </summary>
public sealed class PurgeAllTmdbShowAlternateOrderingsAction(TmdbMetadataUpdater tmdbUpdater) : IScheduledAction
{
    public string Name => "Purge TMDB Show Alternate Orderings";

    public string? Description => "Remove all TMDB show alternate orderings (episode groups) from the local database.";

    public ActionCategory Category => ActionCategory.TMDB;

    public bool RequiresConfirmation => true;

    public string? ConfirmationMessage => "Are you sure you want to remove all TMDB show alternate orderings from the database?";

    public Task Execute(IProgress<decimal> progress, CancellationToken token)
    {
        tmdbUpdater.PurgeAllShowEpisodeGroups(progress, token);
        return Task.CompletedTask;
    }
}
