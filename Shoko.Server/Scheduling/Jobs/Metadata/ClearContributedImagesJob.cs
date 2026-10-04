using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Services;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Workers;

namespace Shoko.Server.Scheduling.Jobs.Metadata;

/// <summary>
///   Removes the image links an image contributor added to entities of the
///   pairs it is no longer enabled for.
/// </summary>
/// <remarks>
///   Reads the contributor's enabled pairs when it runs, so it removes what
///   the settings no longer allow, whatever changed since it was queued. The
///   links on the contributor's own source are its provider's and are left
///   alone. The images themselves go with the orphaned image purge.
/// </remarks>
/// <param name="contributorManager">Holds the contributors' enabled pairs.</param>
/// <param name="imageManager">Reads and removes the links.</param>
/// <param name="cancellationAccessor">Cancels the work.</param>
[DatabaseRequired]
[JobKeyGroup(JobKeyGroup.Metadata)]
// Not default + 50: a prioritized cleanup only moves ahead of other cleanup.
[JobPriority(Default = 0, Prioritized = 10)]
public class ClearContributedImagesJob(
    IMetadataImageContributorManager contributorManager,
    IImageManager imageManager,
    IJobCancellationAccessor cancellationAccessor
) : BaseJob
{
    #region Properties

    /// <summary>
    ///   The contributor whose links are removed.
    /// </summary>
    public Guid ContributorID { get; set; }

    /// <summary>
    ///   The source the contributor keeps its images and links under, kept
    ///   so a contributor that is gone by the time the job runs is still
    ///   cleared.
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <inheritdoc />
    public override string TypeName => "Clear Contributed Images";

    /// <inheritdoc />
    public override string Title => "Clearing Contributed Images";

    #endregion

    #region Execution

    /// <inheritdoc />
    public override Task Execute()
    {
        if (!MetadataSource.TryGet(Source, out var source))
        {
            _logger.LogWarning("Not clearing contributed images under {Source}, which is not a registered source.", Source);
            return Task.CompletedTask;
        }

        var token = cancellationAccessor.Token;
        var enabled = contributorManager.GetImageContributorInfo(ContributorID) is { } info && info.Source == source
            ? info.EnabledScope
            : MetadataEntityScope.Empty;
        var removed = 0;
        var xrefs = imageManager.GetAllImageCrossReferences(new() { ImageSource = source, XrefSource = source })
            .Where(xref => xref.EntityID.Source != source && !enabled.Contains(xref.EntityID))
            .ToList();
        foreach (var xref in xrefs)
        {
            token.ThrowIfCancellationRequested();
            if (imageManager.RemoveImageCrossReference(xref))
                removed++;
        }

        _logger.LogInformation("Removed {Count} image links contributed under {Source}.", removed, source);
        return Task.CompletedTask;
    }

    #endregion
}
