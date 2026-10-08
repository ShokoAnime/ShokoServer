using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Server.Settings;

namespace Shoko.Server.Actions;

/// <summary>
///   Purge the images of one metadata source, or of every source, that
///   nothing links to any more.
/// </summary>
/// <remarks>
///   Only queues the purge, which runs on its own.
/// </remarks>
/// <param name="imageManager">Queues the purge.</param>
/// <param name="settingsProvider">Gives the default minimum age.</param>
public sealed class PurgeOrphanedImagesAction(IImageManager imageManager, ISettingsProvider settingsProvider) : IExecutableAction
{
    /// <summary>
    ///   The source to purge, or <c>null</c> for every source.
    /// </summary>
    public MetadataSource? Source { get; set; }

    /// <summary>
    ///   How many days an image must have gone unused before it is purged, or
    ///   <c>null</c> to follow <see cref="MetadataSettings.PurgeOrphanedAfterDays"/>.
    /// </summary>
    [Range(0, MetadataSettings.MaxPurgeOrphanedAfterDays)]
    public int? MinimumAgeInDays { get; set; }

    public string Name => "Purge Orphaned Images";

    public string? Description
        => "Removes the images of one metadata source, or of every source, that are not linked to anything.";

    public ActionCategory Category => ActionCategory.Destructive;

    public ActionPermission Permission => ActionPermission.Admin;

    public bool RequiresConfirmation => true;

    public string? ConfirmationMessage => "Are you sure you want to remove the unlinked images of the chosen metadata sources?";

    public Task<ActionValidationResult?> Validate(CancellationToken token = default)
        => Task.FromResult(
            MinimumAgeInDays is < 0 or > MetadataSettings.MaxPurgeOrphanedAfterDays
                ? new ActionValidationResult($"The minimum age must be between 0 and {MetadataSettings.MaxPurgeOrphanedAfterDays} days.")
                : MetadataPurges.Check(Source, _ => true, "images")
        );

    public Task Execute(CancellationToken token = default)
        => imageManager.SchedulePurgeOfOrphanedImages(
            MinimumAgeInDays ?? settingsProvider.GetSettings().Metadata.PurgeOrphanedAfterDays,
            Source
        );
}
