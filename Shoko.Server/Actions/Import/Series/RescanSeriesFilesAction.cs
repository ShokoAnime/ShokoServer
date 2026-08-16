using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Video.Services;
using Shoko.Server.Models.Shoko;

namespace Shoko.Server.Actions;

/// <summary>
///   Rescan all files for the series, re-running release matching.
/// </summary>
public sealed class RescanSeriesFilesAction(IVideoReleaseService releaseService) : SeriesAction
{
    public override string Name => "Rescan Files";

    public string? Description => "Rescans every file associated with the series.";

    public ActionCategory Category => ActionCategory.Import;

    public override ActionPermission Permission => ActionPermission.Admin;

    public override async Task Execute(CancellationToken token = default)
    {
        var animeSeries = (AnimeSeries)Series;
        foreach (var file in animeSeries.VideoLocals)
            await releaseService.ScheduleFindReleaseForVideo(file, force: true);
    }
}
