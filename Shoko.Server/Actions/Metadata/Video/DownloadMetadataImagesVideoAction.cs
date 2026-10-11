using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Download the images the image contributors supply for the video.
/// </summary>
public sealed class DownloadMetadataImagesVideoAction(IMetadataRefreshService refreshService) : VideoAction
{
    /// <summary>
    ///   Download the desired images again even when they are already there.
    /// </summary>
    public bool Force { get; set; }

    public override string Name => "Download Images";

    public override string? Description => "Downloads the images the image contributors supply for the video.";

    public override ActionCategory Category => ActionCategory.Images;

    public override ActionPermission Permission => ActionPermission.Admin;

    public override Task Execute(CancellationToken token = default)
        => refreshService.DownloadImages(Video.ID, Force, cancellationToken: token);
}
