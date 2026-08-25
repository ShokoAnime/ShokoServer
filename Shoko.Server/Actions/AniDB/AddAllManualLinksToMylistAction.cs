using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata.Anidb.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Forcibly runs AddToMylist commands for all manually linked files.
/// </summary>
public sealed class AddAllManualLinksToMylistAction(IMylistService mylistService) : IExecutableAction
{
    public string Name => "Add All Manual Links to Mylist";

    public string? Description => "Forcibly run AddToMylist commands for all files with manual links.";

    public ActionCategory Category => ActionCategory.AniDB;

    public ActionPermission Permission => ActionPermission.Admin;

    public Task Execute(CancellationToken token = default)
        => mylistService.ScheduleAddAllManualLinks();
}
