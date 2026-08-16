using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Schedule auto-downloads for all missing images across all entities.
/// </summary>
public sealed class UpdateAllImagesAction(IImageManager imageManager) : IExecutableAction
{
    public string Name => "Update All Images";

    public string? Description => "Schedule downloads for all missing images across all entities.";

    public ActionCategory Category => ActionCategory.Images;

    public ActionPermission Permission => ActionPermission.User;

    public Task Execute(CancellationToken token = default)
        => imageManager.ScheduleAllAutoDownloads();
}
