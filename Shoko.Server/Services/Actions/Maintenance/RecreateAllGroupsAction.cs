using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Action;
using Shoko.Abstractions.Action.Enums;
using Shoko.Server.Tasks;

namespace Shoko.Server.Services.Actions.Maintenance;

/// <summary>
///   Delete all existing groups and recreate them from scratch based on
///   current settings.
/// </summary>
internal sealed class RecreateAllGroupsAction(AnimeGroupCreator groupCreator) : IExecutableGlobalSystemAction
{
    public string Name => "Recreate All Groups";
    public string? Description => "Delete all groups and recreate them from scratch based on current settings.";
    public ActionCategory Category => ActionCategory.Maintenance;
    public bool RequiresConfirmation => true;

    public async Task Execute(CancellationToken cancellationToken = default)
    {
        await groupCreator.RecreateAllGroups();
    }
}
