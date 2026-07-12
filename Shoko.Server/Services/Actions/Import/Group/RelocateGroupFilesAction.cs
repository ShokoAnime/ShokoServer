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
///   Relocate all files for the group.
/// </summary>
internal sealed class RelocateGroupFilesAction(
    AnimeGroupRepository groupRepo,
    IVideoRelocationService relocationService
) : IExecutableGroupAction
{
    public string Name => "Relocate Files";
    public string? Description => "Renames and/or moves every file associated with the group.";
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
            await relocationService.ScheduleAutoRelocationForVideo(file);
    }
}
