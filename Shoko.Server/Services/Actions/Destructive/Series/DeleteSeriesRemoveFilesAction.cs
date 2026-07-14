using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Action;
using Shoko.Abstractions.Action.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Services;

namespace Shoko.Server.Services.Actions.Destructive.Series;

/// <summary>
///   Delete the series along with its files from disk.
/// </summary>
internal sealed class DeleteSeriesRemoveFilesAction(
    AnimeSeriesRepository seriesRepo,
    AnimeSeriesService seriesService
) : IExecutableSeriesSystemAction
{
    public string Name => "Delete Series - Remove Files";
    public string? Description => "Deletes the series from Shoko along with the files.";
    public ActionCategory Category => ActionCategory.Destructive;
    public bool RequiresConfirmation => true;

    public async Task Execute(IShokoSeries series, CancellationToken cancellationToken = default)
    {
        var animeSeries = seriesRepo.GetByAnimeID(series.AnidbAnimeID);
        if (animeSeries is null) return;
        await seriesService.DeleteSeries(animeSeries, true, true, false);
    }
}
