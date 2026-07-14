using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Action;
using Shoko.Abstractions.Action.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Video.Services;
using Shoko.Server.Repositories.Cached;

namespace Shoko.Server.Services.Actions.Import.Series;

/// <summary>
///   Relocate all files for the series.
/// </summary>
public sealed class RelocateSeriesFilesAction(
    AnimeSeriesRepository seriesRepo,
    IVideoRelocationService relocationService
) : IExecutableSeriesSystemAction
{
    public string Name => "Relocate Files";
    public string? Description => "Renames and/or moves every file associated with the series.";
    public ActionCategory Category => ActionCategory.Import;

    public async Task Execute(IShokoSeries series, CancellationToken cancellationToken = default)
    {
        var animeSeries = seriesRepo.GetByAnimeID(series.AnidbAnimeID);
        if (animeSeries is null) return;

        foreach (var file in animeSeries.VideoLocals)
            await relocationService.ScheduleAutoRelocationForVideo(file);
    }
}
