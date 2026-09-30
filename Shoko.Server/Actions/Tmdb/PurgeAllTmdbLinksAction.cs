using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.ScheduledActions;

namespace Shoko.Server.Actions;

/// <summary>
///   Remove all AniDB-TMDB show and movie links, leaving the auto-linking
///   state of each anime as it was.
/// </summary>
public sealed class PurgeAllTmdbLinksAction(IMetadataLinkingService linkingService) : IScheduledAction
{
    public string Name => "Purge All TMDB Links";

    public string? Description => "Remove all AniDB-TMDB links.";

    public ActionCategory Category => ActionCategory.TMDB;

    public bool RequiresConfirmation => true;

    public string? ConfirmationMessage => "Are you sure you want to remove all AniDB-TMDB links from the database?";

    public Task Execute(IProgress<decimal> progress, CancellationToken token)
        => linkingService.RemoveAllLinks(MetadataSource.TMDB, cancellationToken: token);
}
