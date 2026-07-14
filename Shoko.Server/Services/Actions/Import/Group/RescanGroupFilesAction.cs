using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Action;
using Shoko.Abstractions.Action.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Video.Services;
using Shoko.Server.Repositories.Cached;

namespace Shoko.Server.Services.Actions.Import.Group;

/// <summary>
///   Rescan all files for the group, re-running release matching.
/// </summary>
public sealed class RescanGroupFilesAction(
    AnimeGroupRepository groupRepo,
    IVideoReleaseService releaseService
) : IExecutableGroupSystemAction
{
    public string Name => "Rescan Files";
    public string? Description => "Rescans every file associated with the group.";
    public ActionCategory Category => ActionCategory.Import;

    public async Task Execute(IShokoGroup group, CancellationToken cancellationToken = default)
    {
        var animeGroup = groupRepo.GetByID(group.ID);
        if (animeGroup is null) return;

        var files = animeGroup.AllSeries
            .SelectMany(s => s.VideoLocals)
            .DistinctBy(v => v.VideoLocalID)
            .ToList();

        foreach (var file in files)
            await releaseService.ScheduleFindReleaseForVideo(file, force: true);
    }
}
