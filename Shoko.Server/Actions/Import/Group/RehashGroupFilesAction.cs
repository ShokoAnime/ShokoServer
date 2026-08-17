using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Scheduling.Jobs.Shoko;

namespace Shoko.Server.Actions;

/// <summary>
///   Rehash all files for the group.
/// </summary>
public sealed class RehashGroupFilesAction(IQueueScheduler scheduler) : GroupAction
{
    public override string Name => "Rehash Files";

    public override string? Description => "Rehashes every file associated with the group.";

    public override ActionCategory Category => ActionCategory.Import;

    public override ActionPermission Permission => ActionPermission.Admin;

    public override async Task Execute(CancellationToken token = default)
    {
        var animeGroup = (AnimeGroup)Group;
        var files = animeGroup.AllSeries
            .SelectMany(s => s.VideoLocals)
            .DistinctBy(v => v.VideoLocalID)
            .ToList();

        foreach (var file in files)
        {
            var filePath = file.FirstResolvedPlace?.Path;
            if (string.IsNullOrEmpty(filePath))
                continue;
            await scheduler.Enqueue<HashFileJob>(c => (c.FilePath, c.ForceHash) = (filePath, true), prioritize: true, ct: token);
        }
    }
}
