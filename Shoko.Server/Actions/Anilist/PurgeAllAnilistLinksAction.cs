using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata.Anilist.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Remove all AniDB-AniList links, optionally resetting the auto-linking state.
/// </summary>
public sealed class PurgeAllAnilistLinksAction(IAnilistLinkingService linkingService) : IExecutableAction
{
    public string Name => "Purge All AniList Links";

    public string? Description => "Remove all AniDB-AniList links and reset the auto-linking state.";

    public ActionCategory Category => ActionCategory.AniList;

    public ActionPermission Permission => ActionPermission.Admin;

    public bool RequiresConfirmation => true;

    public string? ConfirmationMessage => "Are you sure you want to remove all AniDB-AniList links from the database?";

    public bool? ResetAutoLinkingState { get; set; }

    public Task Execute(CancellationToken token = default)
    {
        linkingService.RemoveAllLinks();
        if (ResetAutoLinkingState.HasValue)
            linkingService.ResetAutoLinkingState(ResetAutoLinkingState.Value);
        return Task.CompletedTask;
    }
}
