using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Action;
using Shoko.Abstractions.Action.Enums;

namespace Shoko.Server.Services.Actions.Tmdb;

/// <summary>
///   Remove all AniDB-TMDB links and reset the auto-linking state.
/// </summary>
internal sealed class PurgeAllTmdbLinksAction(ActionService actionService) : IExecutableGlobalAction
{
    public string Name => "Purge All TMDB Links";
    public string? Description => "Remove all AniDB-TMDB links and reset the auto-linking state.";
    public ActionCategory Category => ActionCategory.TMDB;
    public bool RequiresConfirmation => true;

    public Task Execute(CancellationToken cancellationToken = default)
        => actionService.PurgeAllTmdbLinks();
}
