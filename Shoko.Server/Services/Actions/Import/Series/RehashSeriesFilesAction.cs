using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Action;
using Shoko.Abstractions.Action.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Scheduling;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Scheduling.Jobs.Shoko;

namespace Shoko.Server.Services.Actions.Import.Series;

/// <summary>
///   Rehash all files for the series.
/// </summary>
public sealed class RehashSeriesFilesAction(
    AnimeSeriesRepository seriesRepo,
    IQueueScheduler scheduler
) : IExecutableSeriesSystemAction
{
    public string Name => "Rehash Files";
    public string? Description => "Rehashes every file associated with the series.";
    public ActionCategory Category => ActionCategory.Import;

    public async Task Execute(IShokoSeries series, CancellationToken cancellationToken = default)
    {
        var animeSeries = seriesRepo.GetByAnimeID(series.AnidbAnimeID);
        if (animeSeries is null) return;

        foreach (var file in animeSeries.VideoLocals)
        {
            var filePath = file.FirstResolvedPlace?.Path;
            if (string.IsNullOrEmpty(filePath))
                continue;
            await scheduler.StartJob<HashFileJob>(c => (c.FilePath, c.ForceHash) = (filePath, true), prioritize: true);
        }
    }
}
