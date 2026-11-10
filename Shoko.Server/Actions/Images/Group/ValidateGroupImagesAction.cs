using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.User.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Validate the images of a group and its series, and re-download the ones
///   found missing or broken.
/// </summary>
/// <param name="imageManager">Validates the images.</param>
/// <param name="actorContext">The current actor, whose visible series are validated.</param>
public sealed class ValidateGroupImagesAction(IImageManager imageManager, IActorContext actorContext)
    : GroupAction, IVisibleSeriesGroupAction, IProgressReportingAction, IImageValidationAction
{
    private IProgress<decimal>? _progress;

    /// <inheritdoc />
    public List<MetadataSource>? ImageSources { get; set; }

    /// <inheritdoc />
    public List<ImageEntityType>? ImageTypes { get; set; }

    /// <inheritdoc />
    public bool ReadContents { get; set; } = true;

    /// <inheritdoc />
    public bool Redownload { get; set; } = true;

    /// <summary>
    ///   Validate the images of the group's series, at any level, too.
    /// </summary>
    public bool IncludeSeries { get; set; } = true;

    /// <summary>
    ///   Validate the images of those series' episodes too.
    /// </summary>
    public bool IncludeEpisodes { get; set; }

    /// <summary>
    ///   Validate the images of the characters and people credited on those
    ///   series too.
    /// </summary>
    public bool IncludeCastAndCrew { get; set; }

    public override string Name => "Validate Images";

    public override string? Description => "Validates the images of the group and, as chosen, of its series, their episodes and their cast and crew, and re-downloads any that are missing or broken.";

    public override ActionCategory Category => ActionCategory.Images;

    public override ActionPermission Permission => ActionPermission.Admin;

    void IProgressReportingAction.SetProgress(IProgress<decimal> progress)
        => _progress = progress;

    public override Task Execute(CancellationToken token = default)
        => imageManager.ValidateImages(this.Pick(Images()), this.GetOptions(), _progress, token);

    private IEnumerable<IImage> Images()
    {
                foreach (var image in Group.GetImages())
            yield return image;
        if (!IncludeSeries)
            yield break;

        foreach (var series in Group.GetVisibleSeries(actorContext))
        {
            foreach (var image in ValidateSeriesImagesAction.Images(series, IncludeEpisodes, IncludeCastAndCrew))
                yield return image;
        }
    }
}
