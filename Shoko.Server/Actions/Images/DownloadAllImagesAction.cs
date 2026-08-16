using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Schedule auto-downloads for all images across all entities, optionally
///   filtered by image source, image type, and/or cross-reference source.
/// </summary>
public sealed class DownloadAllImagesAction(IImageManager imageManager) : IExecutableAction
{
    public DataSource? ImageSource { get; set; }

    public ImageEntityType? ImageType { get; set; }

    public DataSource? XrefSource { get; set; }

    public bool Force { get; set; }

    public string Name => "Download All Images";

    public string? Description => "Schedule downloads for all images across all entities, optionally filtered by source, type, and cross-reference source.";

    public ActionCategory Category => ActionCategory.Images;

    public ActionPermission Permission => ActionPermission.User;

    public Task Execute(CancellationToken token = default)
        => imageManager.ScheduleAllAutoDownloads(ImageSource, ImageType, XrefSource, Force);
}
