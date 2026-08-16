using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Video.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Scan managed folders for new files and import them without running the
///   full metadata/image pipeline.
/// </summary>
public sealed class ImportNewFilesAction(IVideoService videoService) : IExecutableAction
{
    public string Name => "Import New Files";

    public string? Description => "Scan managed folders for new files, hash them, and find releases.";

    public ActionCategory Category => ActionCategory.Import;

    // The legacy ImportNewFiles endpoint had no admin gate; keep User-level to avoid regressing callers.
    public ActionPermission Permission => ActionPermission.User;

    public Task Execute(CancellationToken token = default)
        => videoService.ScheduleScanForManagedFolders(onlyNewFiles: true);
}
