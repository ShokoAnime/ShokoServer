using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Abstractions.Metadata.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Validate the images of a video, and re-download the ones found missing
///   or broken.
/// </summary>
/// <param name="imageManager">Validates the images.</param>
public sealed class ValidateVideoImagesAction(IImageManager imageManager) : VideoAction, IProgressReportingAction, IImageValidationAction
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
    ///   Validate the images of the episodes the video is linked to too.
    /// </summary>
    public bool IncludeEpisodes { get; set; } = true;

    /// <summary>
    ///   Validate the images of those episodes' series too, without their
    ///   other episodes.
    /// </summary>
    public bool IncludeSeries { get; set; }

    /// <summary>
    ///   Validate the images of the characters and people credited on those
    ///   episodes too.
    /// </summary>
    public bool IncludeCastAndCrew { get; set; }

    public override string Name => "Validate Images";

    public override string? Description => "Validates the images of the video and, as chosen, of its episodes, their series and their cast and crew, and re-downloads any that are missing or broken.";

    public override ActionCategory Category => ActionCategory.Images;

    public override ActionPermission Permission => ActionPermission.Admin;

    void IProgressReportingAction.SetProgress(IProgress<decimal> progress)
        => _progress = progress;

    public override Task Execute(CancellationToken token = default)
        => imageManager.ValidateImages(this.Pick(Images()), this.GetOptions(), _progress, token);

    private IEnumerable<IImage> Images()
    {
                IEnumerable<IImage> images = Video.GetImages();
        if (IncludeEpisodes)
            images = images.Concat(Video.Episodes.SelectMany(episode => ValidateEpisodeImagesAction.Images(episode, IncludeSeries, IncludeCastAndCrew)));
        return images;
    }
}
