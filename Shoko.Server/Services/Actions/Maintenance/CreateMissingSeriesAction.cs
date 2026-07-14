using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Action;
using Shoko.Abstractions.Action.Enums;

namespace Shoko.Server.Services.Actions.Maintenance;

/// <summary>
///   Create anime series entries for files that have release info but no
///   corresponding series.
/// </summary>
internal sealed class CreateMissingSeriesAction(ActionService actionService) : IExecutableGlobalSystemAction
{
    public string Name => "Create Missing Series";
    public string? Description => "Create series entries for files that have release info but no corresponding series.";
    public ActionCategory Category => ActionCategory.Maintenance;

    public async Task Execute(CancellationToken cancellationToken = default)
    {
        await actionService.CreateMissingSeries();
    }
}
