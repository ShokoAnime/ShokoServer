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
///   Validate the images of a series, and re-download the ones found missing
///   or broken.
/// </summary>
/// <param name="imageManager">Validates the images.</param>
public sealed class ValidateSeriesImagesAction(IImageManager imageManager) : SeriesAction, IProgressReportingAction, IImageValidationAction
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
    ///   Validate the images of the series' episodes too.
    /// </summary>
    public bool IncludeEpisodes { get; set; } = true;

    /// <summary>
    ///   Validate the images of the characters and people credited on the
    ///   series and its episodes too.
    /// </summary>
    public bool IncludeCastAndCrew { get; set; } = true;

    public override string Name => "Validate Images";

    public override string? Description => "Validates the images of the series and, as chosen, of its episodes and its cast and crew, and re-downloads any that are missing or broken.";

    public override ActionCategory Category => ActionCategory.Images;

    public override ActionPermission Permission => ActionPermission.Admin;

    void IProgressReportingAction.SetProgress(IProgress<decimal> progress)
        => _progress = progress;

    public override Task Execute(CancellationToken token = default)
        => imageManager.ValidateImages(this.Pick(Images(Series, IncludeEpisodes, IncludeCastAndCrew)), this.GetOptions(), _progress, token);

    /// <summary>
    ///   The images of a series to validate, shared with the group level.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <param name="includeEpisodes">Include the images of its episodes.</param>
    /// <param name="includeCastAndCrew">Include the images of its cast and crew, and its episodes' when they are included.</param>
    /// <returns>The images.</returns>
    internal static IEnumerable<IImage> Images(IShokoSeries series, bool includeEpisodes, bool includeCastAndCrew)
    {
        IEnumerable<IImage> images = series.GetImages();
        if (includeCastAndCrew)
            images = images.Concat(ImageValidationActionExtensions.CastAndCrewImagesOf(series));
        if (includeEpisodes)
            images = images.Concat(series.Episodes.SelectMany(episode => ValidateEpisodeImagesAction.Images(episode, includeSeries: false, includeCastAndCrew)));
        return images;
    }
}
