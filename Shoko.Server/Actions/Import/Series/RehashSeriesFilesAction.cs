using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.Shoko;

namespace Shoko.Server.Actions;

/// <summary>
///   Rehash all files for the series.
/// </summary>
public sealed class RehashSeriesFilesAction(IQueueScheduler scheduler) : SeriesAction
{
    public override string Name => "Rehash Files";

    public override string? Description => "Rehashes every file associated with the series.";

    public override ActionCategory Category => ActionCategory.Import;

    public override ActionPermission Permission => ActionPermission.Admin;

    public override async Task Execute(CancellationToken token = default)
    {
        var animeSeries = (AnimeSeries)Series;
        foreach (var file in animeSeries.VideoLocals)
        {
            var filePath = file.FirstResolvedPlace?.Path;
            if (string.IsNullOrEmpty(filePath))
                continue;
            await scheduler.EnqueueWithPriority<HashFileJob>(
                c => (c.FilePath, c.ForceHash) = (filePath, true),
                JobPriorities.ForFileSize(file.FileSize, isNew: false, prioritize: true),
                ct: token
            );
        }
    }
}
