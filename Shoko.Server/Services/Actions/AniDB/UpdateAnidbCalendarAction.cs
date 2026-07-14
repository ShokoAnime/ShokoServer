using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Action;
using Shoko.Abstractions.Action.Enums;

namespace Shoko.Server.Services.Actions.AniDB;

/// <summary>
///   Update the AniDB calendar data for use on the dashboard.
/// </summary>
internal sealed class UpdateAnidbCalendarAction(ActionService actionService) : IExecutableGlobalSystemAction
{
    public string Name => "Update AniDB Calendar";
    public string? Description => "Update the AniDB calendar data for use on the dashboard.";
    public ActionCategory Category => ActionCategory.AniDB;

    public Task Execute(CancellationToken cancellationToken = default)
        => actionService.UpdateAniDBCalendar();
}
