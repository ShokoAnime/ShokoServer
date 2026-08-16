using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Delete the series along with its files from disk.
/// </summary>
public sealed class DeleteSeriesRemoveFilesAction(AnimeSeriesService seriesService) : SeriesAction
{
    public override string Name => "Delete Series - Remove Files";

    public string? Description => "Deletes the series from Shoko along with the files.";

    public ActionCategory Category => ActionCategory.Destructive;

    public override ActionPermission Permission => ActionPermission.Admin;

    public bool RequiresConfirmation => true;

    public override Task Execute(CancellationToken token = default)
        => seriesService.DeleteSeries((AnimeSeries)Series, deleteFiles: true, updateGroups: true, completelyRemove: false);
}
