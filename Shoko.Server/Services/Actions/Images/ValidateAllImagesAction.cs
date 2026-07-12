using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Action;
using Shoko.Abstractions.Action.Enums;
using Shoko.Abstractions.Metadata.Services;

namespace Shoko.Server.Services.Actions.Images;

/// <summary>
///   Validate all images and re-download any that are corrupted or invalid.
/// </summary>
internal sealed class ValidateAllImagesAction(IImageManager imageManager) : IExecutableGlobalAction
{
    public string Name => "Validate All Images";
    public string? Description => "Validate all images and re-download any that are corrupted or invalid.";
    public ActionCategory Category => ActionCategory.Images;

    public async Task Execute(CancellationToken cancellationToken = default)
    {
        await imageManager.ScheduleValidateAllImages(prioritize: true);
    }
}
