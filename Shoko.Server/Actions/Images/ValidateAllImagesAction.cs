using Shoko.Abstractions.Actions;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Scheduling.Jobs.Image;

namespace Shoko.Server.Actions;

/// <summary>
///   Validate all images and re-download any that are corrupted or invalid.
/// </summary>
/// <remarks>
///   A run is the validation job itself, ahead of the jobs waiting, so it
///   shows the job's progress and an admin can cancel it.
/// </remarks>
/// <param name="scheduler">The queue.</param>
public sealed class ValidateAllImagesAction(IQueueScheduler scheduler) : QueueJobScheduledAction<ValidateAllImagesJob>(scheduler)
{
    public override string Name => "Validate All Images";

    public override string? Description => "Validate all images and re-download any that are corrupted or invalid.";

    public override ActionCategory Category => ActionCategory.Images;

    protected override bool Prioritize => true;
}
