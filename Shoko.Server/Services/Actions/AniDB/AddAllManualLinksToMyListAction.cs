using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Action;
using Shoko.Abstractions.Action.Enums;

namespace Shoko.Server.Services.Actions.AniDB;

/// <summary>
///   Forcibly runs AddToMyList commands for all manually linked files.
/// </summary>
internal sealed class AddAllManualLinksToMyListAction(ActionService actionService) : IExecutableGlobalAction
{
    public string Name => "Add All Manual Links to MyList";
    public string? Description => "Forcibly run AddToMyList commands for all files with manual links.";
    public ActionCategory Category => ActionCategory.AniDB;

    public Task Execute(CancellationToken cancellationToken = default)
        => actionService.AddAllManualLinksToMyList();
}
