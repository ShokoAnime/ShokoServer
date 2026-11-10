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
using Shoko.Abstractions.Metadata.Shoko;

namespace Shoko.Server.Actions;

/// <summary>
///   Validate the images of an episode, and re-download the ones found
///   missing or broken.
/// </summary>
/// <param name="imageManager">Validates the images.</param>
public sealed class ValidateEpisodeImagesAction(IImageManager imageManager) : EpisodeAction, IProgressReportingAction, IImageValidationAction
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
    ///   Validate the images of the episode's series too, without its other
    ///   episodes.
    /// </summary>
    public bool IncludeSeries { get; set; }

    /// <summary>
    ///   Validate the images of the characters and people credited on the
    ///   episode too.
    /// </summary>
    public bool IncludeCastAndCrew { get; set; } = true;

    public override string Name => "Validate Images";

    public override string? Description => "Validates the images of the episode and, as chosen, of its series and its cast and crew, and re-downloads any that are missing or broken.";

    public override ActionCategory Category => ActionCategory.Images;

    public override ActionPermission Permission => ActionPermission.Admin;

    void IProgressReportingAction.SetProgress(IProgress<decimal> progress)
        => _progress = progress;

    public override Task Execute(CancellationToken token = default)
        => imageManager.ValidateImages(this.Pick(Images(Episode, IncludeSeries, IncludeCastAndCrew)), this.GetOptions(), _progress, token);

    /// <summary>
    ///   The images of an episode to validate, shared with the series and
    ///   video levels.
    /// </summary>
    /// <param name="episode">The episode.</param>
    /// <param name="includeSeries">Include the images of its series, without its other episodes.</param>
    /// <param name="includeCastAndCrew">Include the images of its cast and crew.</param>
    /// <returns>The images.</returns>
    internal static IEnumerable<IImage> Images(IShokoEpisode episode, bool includeSeries, bool includeCastAndCrew)
    {
        IEnumerable<IImage> images = episode.GetImages();
        if (includeCastAndCrew)
            images = images.Concat(ImageValidationActionExtensions.CastAndCrewImagesOf(episode));
        if (includeSeries && episode.Series is { } series)
            images = images.Concat(series.GetImages());
        return images;
    }
}
