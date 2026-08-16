using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Scheduling.Jobs.Actions;

namespace Shoko.Server.Actions;

/// <summary>
///   Run the full import pipeline: scan for new files, hash them, find
///   releases, update metadata, and download missing images.
/// </summary>
public sealed class RunImportAction(IQueueScheduler scheduler) : IExecutableAction
{
    public string Name => "Run Import";

    public string? Description => "Check for new files, hash them, scan for metadata matches, and download missing images.";

    public ActionCategory Category => ActionCategory.Import;

    // The legacy RunImport endpoint had no admin gate; keep User-level to avoid regressing callers.
    public ActionPermission Permission => ActionPermission.User;

    public Task Execute(CancellationToken token = default)
        => scheduler.Enqueue<ImportJob>(ct: token);
}
