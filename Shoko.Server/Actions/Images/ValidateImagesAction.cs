using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Validate every image in the collection, or those from some sources or of
///   some types, and re-download the ones found missing or broken.
/// </summary>
/// <remarks>
///   Validates in the run itself, with its progress; the downloads it queues
///   run on their own. <see cref="ValidateAllImagesAction"/> is the scheduled
///   form, with the default options.
/// </remarks>
/// <param name="imageManager">Validates the images.</param>
public sealed class ValidateImagesAction(IImageManager imageManager) : IExecutableAction, IProgressReportingAction, IImageValidationAction
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

    public string Name => "Validate Images";

    public string? Description => "Validates every image, or those from some sources or of some types, and re-downloads any that are missing or broken.";

    public ActionCategory Category => ActionCategory.Images;

    public ActionPermission Permission => ActionPermission.Admin;

    void IProgressReportingAction.SetProgress(IProgress<decimal> progress)
        => _progress = progress;

    public Task Execute(CancellationToken token = default)
    {
        // A stored image has a type only for each entity it is linked to, so the types pick the images linked as them.
        var images = ImageTypes is { Count: > 0 } types
            ? types.Distinct().SelectMany(type => imageManager.GetAllImages(new() { ImageType = type }))
            : imageManager.GetAllImages();
        if (ImageSources is { Count: > 0 } list && list.ToHashSet() is var sources)
            images = images.Where(image => sources.Contains(image.Source));
        return imageManager.ValidateImages(images, this.GetOptions(), _progress, token);
    }
}
