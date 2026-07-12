using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Action;
using Shoko.Abstractions.Action.Enums;
using Shoko.Abstractions.Video.Services;

namespace Shoko.Server.Services.Actions.Import;

/// <summary>
///   Scan managed folders for new files and import them without running the
///   full metadata/image pipeline.
/// </summary>
internal sealed class ImportNewFilesAction(IVideoService videoService) : IExecutableGlobalAction
{
    public string Name => "Import New Files";
    public string? Description => "Scan managed folders for new files, hash them, and find releases.";
    public ActionCategory Category => ActionCategory.Import;

    public async Task Execute(CancellationToken cancellationToken = default)
    {
        await videoService.ScheduleScanForManagedFolders(onlyNewFiles: true);
    }
}
