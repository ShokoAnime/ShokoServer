using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Action;
using Shoko.Abstractions.Action.Enums;
using Shoko.Abstractions.Metadata.Services;

namespace Shoko.Server.Services.Actions.Images;

/// <summary>
///   Schedule auto-downloads for all missing images across all entities.
/// </summary>
internal sealed class UpdateAllImagesAction(IImageManager imageManager) : IExecutableGlobalSystemAction
{
    public string Name => "Update All Images";
    public string? Description => "Schedule downloads for all missing images across all entities.";
    public ActionCategory Category => ActionCategory.Images;

    public async Task Execute(CancellationToken cancellationToken = default)
    {
        await imageManager.ScheduleAllAutoDownloads();
    }
}
