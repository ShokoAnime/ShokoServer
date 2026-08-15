using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Server.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Update AniDB release info for files with missing or incomplete group data.
/// </summary>
public sealed class UpdateMissingAnidbFileInfoAction(ActionService actionService) : IExecutableAction
{
    public string Name => "Update Missing AniDB File Info";

    public string? Description => "Update AniDB release info for files with missing or incomplete group information.";

    public ActionCategory Category => ActionCategory.AniDB;

    // Matches today: [Authorize("admin")] on the legacy UpdateMissingAniDBFileInfo endpoint.
    public ActionPermission Permission => ActionPermission.Admin;

    public Task Execute(CancellationToken token = default)
        => actionService.UpdateAnidbReleaseInfo();
}
