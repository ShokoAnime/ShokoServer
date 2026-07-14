using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Action;
using Shoko.Abstractions.Action.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Services;

namespace Shoko.Server.Services.Actions.Destructive.Series;

/// <summary>
///   Completely remove all data and files for the series.
/// </summary>
public sealed class DeleteSeriesAllDataAction(
    AnimeSeriesRepository seriesRepo,
    AnimeSeriesService seriesService
) : IExecutableSeriesSystemAction
{
    public string Name => "Delete Series - All Series Data and Files";
    public string? Description => "Removes ALL DATA AND FILES relating to the series. Use with caution, as you may get temp banned from AniDB if it's abused.";
    public ActionCategory Category => ActionCategory.Destructive;
    public bool RequiresConfirmation => true;

    public async Task Execute(IShokoSeries series, CancellationToken cancellationToken = default)
    {
        var animeSeries = seriesRepo.GetByAnimeID(series.AnidbAnimeID);
        if (animeSeries is null) return;
        await seriesService.DeleteSeries(animeSeries, true, true, true);
    }
}
