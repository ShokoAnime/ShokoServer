using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.ScheduledActions;

namespace Shoko.Server.Actions;

/// <summary>
///   Validate all images and re-download any that are corrupted or invalid.
/// </summary>
public sealed class ValidateAllImagesAction(IImageManager imageManager) : IScheduledAction
{
    public string Name => "Validate All Images";

    public string? Description => "Validate all images and re-download any that are corrupted or invalid.";

    public ActionCategory Category => ActionCategory.Images;

    public Task Execute(IProgress<decimal> progress, CancellationToken token)
        => imageManager.ScheduleValidateAllImages(prioritize: true);
}
