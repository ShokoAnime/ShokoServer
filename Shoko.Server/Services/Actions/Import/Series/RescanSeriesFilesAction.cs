using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Action;
using Shoko.Abstractions.Action.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Video.Services;
using Shoko.Server.Repositories.Cached;

namespace Shoko.Server.Services.Actions.Import.Series;

/// <summary>
///   Rescan all files for the series, re-running release matching.
/// </summary>
internal sealed class RescanSeriesFilesAction(
    AnimeSeriesRepository seriesRepo,
    IVideoReleaseService releaseService
) : IExecutableSeriesAction
{
    public string Name => "Rescan Files";
    public string? Description => "Rescans every file associated with the series.";
    public ActionCategory Category => ActionCategory.Import;

    public async Task Execute(IShokoSeries series, CancellationToken cancellationToken = default)
    {
        var animeSeries = seriesRepo.GetByAnimeID(series.AnidbAnimeID);
        if (animeSeries is null) return;

        foreach (var file in animeSeries.VideoLocals)
            await releaseService.ScheduleFindReleaseForVideo(file, force: true);
    }
}
