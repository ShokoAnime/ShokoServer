using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata.Tmdb.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Remove all AniDB-TMDB links and optionally reset the auto-linking state.
/// </summary>
public sealed class PurgeAllTmdbLinksAction(ITmdbLinkingService linkingService) : IExecutableAction
{
    public string Name => "Purge All TMDB Links";

    public string? Description => "Remove all AniDB-TMDB links and reset the auto-linking state.";

    public ActionCategory Category => ActionCategory.TMDB;

    public ActionPermission Permission => ActionPermission.Admin;

    public bool RequiresConfirmation => true;

    public string? ConfirmationMessage => "Are you sure you want to remove all AniDB-TMDB links from the database?";

    /// <summary>Whether to remove show links.</summary>
    public bool RemoveShowLinks { get; set; } = true;

    /// <summary>Whether to remove movie links.</summary>
    public bool RemoveMovieLinks { get; set; } = true;

    /// <summary>Whether to reset the auto-linking state.</summary>
    public bool? ResetAutoLinkingState { get; set; }

    public Task Execute(CancellationToken token = default)
    {
        if (RemoveShowLinks || RemoveMovieLinks)
            linkingService.RemoveAllLinks(RemoveShowLinks, RemoveMovieLinks);
        if (ResetAutoLinkingState.HasValue)
            linkingService.ResetAutoLinkingState(ResetAutoLinkingState.Value);
        return Task.CompletedTask;
    }
}
