using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.ScheduledActions;

namespace Shoko.Server.Actions;

/// <summary>
///   Purge all TMDB movie collections from the local database.
/// </summary>
public sealed class PurgeAllTmdbMovieCollectionsAction(IMetadataPurgeService purgeService) : IScheduledAction
{
    public string Name => "Purge TMDB Movie Collections";

    public string? Description => "Remove all TMDB movie collections from the local database.";

    public ActionCategory Category => ActionCategory.TMDB;

    public bool RequiresConfirmation => true;

    public string? ConfirmationMessage => "Are you sure you want to remove all TMDB movie collections from the database?";

    public Task Execute(IProgress<decimal> progress, CancellationToken token)
        => purgeService.PurgeCollections(MetadataSource.TMDB, token);
}
