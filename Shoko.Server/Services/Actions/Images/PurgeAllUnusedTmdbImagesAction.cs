using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Action;
using Shoko.Abstractions.Action.Enums;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;

namespace Shoko.Server.Services.Actions.Images;

/// <summary>
///   Purge all unused TMDB images that are not linked to any entity.
/// </summary>
public sealed class PurgeAllUnusedTmdbImagesAction(IImageManager imageManager) : IExecutableGlobalSystemAction
{
    public string Name => "Purge Unused TMDB Images";
    public string? Description => "Remove all TMDB images that are not linked to any entity.";
    public ActionCategory Category => ActionCategory.Images;
    public bool RequiresConfirmation => true;

    public async Task Execute(CancellationToken cancellationToken = default)
    {
        await imageManager.SchedulePurgeOfOrphanedImages(0, DataSource.TMDB);
    }
}
