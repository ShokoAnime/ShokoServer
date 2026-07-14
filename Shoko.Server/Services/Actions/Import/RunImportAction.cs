using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Action;
using Shoko.Abstractions.Action.Enums;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Scheduling.Jobs.Actions;

namespace Shoko.Server.Services.Actions.Import;

/// <summary>
///   Run the full import pipeline: scan for new files, hash them, find releases,
///   update metadata, and download missing images.
/// </summary>
public sealed class RunImportAction(IJobFactory jobFactory) : IExecutableGlobalSystemAction
{
    public string Name => "Run Import";
    public string? Description => "Check for new files, hash them, scan for metadata matches, and download missing images.";
    public ActionCategory Category => ActionCategory.Import;

    public async Task Execute(CancellationToken cancellationToken = default)
    {
        await jobFactory.Execute<ImportJob>();
    }
}
