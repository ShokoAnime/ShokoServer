using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Video.Services;
using Shoko.Server.Models.Shoko;

namespace Shoko.Server.Actions;

/// <summary>
///   Rescan all files for the group, re-running release matching.
/// </summary>
public sealed class RescanGroupFilesAction(IVideoReleaseService releaseService) : GroupAction
{
    public override string Name => "Rescan Files";

    public string? Description => "Rescans every file associated with the group.";

    public ActionCategory Category => ActionCategory.Import;

    public override ActionPermission Permission => ActionPermission.Admin;

    public override async Task Execute(CancellationToken token = default)
    {
        var animeGroup = (AnimeGroup)Group;
        var files = animeGroup.AllSeries
            .SelectMany(s => s.VideoLocals)
            .DistinctBy(v => v.VideoLocalID)
            .ToList();

        foreach (var file in files)
            await releaseService.ScheduleFindReleaseForVideo(file, force: true);
    }
}
