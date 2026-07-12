using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Action;
using Shoko.Abstractions.Action.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Scheduling;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Scheduling.Jobs.Shoko;

namespace Shoko.Server.Services.Actions.Import.Group;

/// <summary>
///   Rehash all files for the group.
/// </summary>
internal sealed class RehashGroupFilesAction(
    AnimeGroupRepository groupRepo,
    IQueueScheduler scheduler
) : IExecutableGroupAction
{
    public string Name => "Rehash Files";
    public string? Description => "Rehashes every file associated with the group.";
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
        {
            var filePath = file.FirstResolvedPlace?.Path;
            if (string.IsNullOrEmpty(filePath))
                continue;
            await scheduler.StartJob<HashFileJob>(c => (c.FilePath, c.ForceHash) = (filePath, true), prioritize: true);
        }
    }
}
