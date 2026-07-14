using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Action;
using Shoko.Abstractions.Action.Enums;

namespace Shoko.Server.Services.Actions.AniDB;

/// <summary>
///   Update AniDB release info for files with missing or incomplete group data.
/// </summary>
internal sealed class UpdateMissingAnidbFileInfoAction(ActionService actionService) : IExecutableGlobalSystemAction
{
    public string Name => "Update Missing AniDB File Info";
    public string? Description => "Update AniDB release info for files with missing or incomplete group information.";
    public ActionCategory Category => ActionCategory.AniDB;

    public async Task Execute(CancellationToken cancellationToken = default)
    {
        await actionService.UpdateAnidbReleaseInfo();
    }
}
