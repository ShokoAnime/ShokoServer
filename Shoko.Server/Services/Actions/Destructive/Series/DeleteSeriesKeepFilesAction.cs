using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Action;
using Shoko.Abstractions.Action.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Services;

namespace Shoko.Server.Services.Actions.Destructive.Series;

/// <summary>
///   Delete the series from Shoko but keep the files on disk.
/// </summary>
public sealed class DeleteSeriesKeepFilesAction(
    AnimeSeriesRepository seriesRepo,
    AnimeSeriesService seriesService
) : IExecutableSeriesSystemAction
{
    public string Name => "Delete Series - Keep Files";
    public string? Description => "Deletes the series from Shoko but does not delete the files. Cached AniDB data is preserved.";
    public ActionCategory Category => ActionCategory.Destructive;
    public bool RequiresConfirmation => true;

    public async Task Execute(IShokoSeries series, CancellationToken cancellationToken = default)
    {
        var animeSeries = seriesRepo.GetByAnimeID(series.AnidbAnimeID);
        if (animeSeries is null) return;
        await seriesService.DeleteSeries(animeSeries, false, true, false);
    }
}
