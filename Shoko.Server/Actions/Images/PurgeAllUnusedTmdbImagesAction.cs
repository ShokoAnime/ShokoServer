using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Purge all unused TMDB images that are not linked to any entity.
/// </summary>
public sealed class PurgeAllUnusedTmdbImagesAction(IImageManager imageManager) : IExecutableAction
{
    public string Name => "Purge Unused TMDB Images";

    public string? Description => "Remove all TMDB images that are not linked to any entity.";

    public ActionCategory Category => ActionCategory.Images;

    public ActionPermission Permission => ActionPermission.Admin;

    public bool RequiresConfirmation => true;

    public string? ConfirmationMessage => "Are you sure you want to remove all unused TMDB images from the database?";

    public Task Execute(CancellationToken token = default)
        => imageManager.SchedulePurgeOfOrphanedImages(0, DataSource.TMDB);
}
