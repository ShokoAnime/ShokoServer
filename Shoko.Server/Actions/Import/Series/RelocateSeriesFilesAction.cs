using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Video.Services;
using Shoko.Server.Models.Shoko;

namespace Shoko.Server.Actions;

/// <summary>
///   Relocate all files for the series.
/// </summary>
public sealed class RelocateSeriesFilesAction(IVideoRelocationService relocationService) : SeriesAction
{
    public override string Name => "Relocate Files";

    public string? Description => "Renames and/or moves every file associated with the series.";

    public ActionCategory Category => ActionCategory.Import;

    public override ActionPermission Permission => ActionPermission.Admin;

    public override async Task Execute(CancellationToken token = default)
    {
        var animeSeries = (AnimeSeries)Series;
        foreach (var file in animeSeries.VideoLocals)
            await relocationService.ScheduleAutoRelocationForVideo(file);
    }
}
