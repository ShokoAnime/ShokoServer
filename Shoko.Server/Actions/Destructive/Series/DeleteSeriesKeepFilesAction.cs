using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Delete the series from Shoko but keep the files on disk.
/// </summary>
public sealed class DeleteSeriesKeepFilesAction(AnimeSeriesService seriesService) : SeriesAction
{
    public override string Name => "Delete Series - Keep Files";

    public string? Description => "Deletes the series from Shoko but does not delete the files. Cached AniDB data is preserved.";

    public ActionCategory Category => ActionCategory.Destructive;

    public override ActionPermission Permission => ActionPermission.Admin;

    public bool RequiresConfirmation => true;

    public override Task Execute(CancellationToken token = default)
        => seriesService.DeleteSeries((AnimeSeries)Series, deleteFiles: false, updateGroups: true, completelyRemove: false);
}
