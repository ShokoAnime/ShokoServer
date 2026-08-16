using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Validate all images and re-download any that are corrupted or invalid.
/// </summary>
public sealed class ValidateAllImagesAction(IImageManager imageManager) : IExecutableAction
{
    public string Name => "Validate All Images";

    public string? Description => "Validate all images and re-download any that are corrupted or invalid.";

    public ActionCategory Category => ActionCategory.Images;

    public ActionPermission Permission => ActionPermission.User;

    public Task Execute(CancellationToken token = default)
        => imageManager.ScheduleValidateAllImages(prioritize: true);
}
