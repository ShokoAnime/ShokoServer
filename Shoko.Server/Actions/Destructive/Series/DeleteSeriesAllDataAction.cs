using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Completely remove all data and files for the series.
/// </summary>
public sealed class DeleteSeriesAllDataAction(AnimeSeriesService seriesService) : SeriesAction
{
    public override string Name => "Delete Series - All Series Data and Files";

    public override string? Description => "Removes ALL DATA AND FILES relating to the series. Use with caution, as you may get temp banned from AniDB if it's abused.";

    public override ActionCategory Category => ActionCategory.Destructive;

    public override ActionPermission Permission => ActionPermission.Admin;

    public override bool RequiresConfirmation => true;

    public override Task Execute(CancellationToken token = default)
        => seriesService.DeleteSeries((AnimeSeries)Series, deleteFiles: true, updateGroups: true, completelyRemove: true);
}
