using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.ScheduledActions;
using Shoko.Server.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Heal the releases linking an episode without its series, fix the file
///   cross-references missing their anime, and queue the metadata files are
///   linked to but that is missing, of AniDB and every other source. Runs at
///   start-up by default.
/// </summary>
/// <remarks>
///   Only queues the refreshes, which run on their own; the progress covers
///   the healing and the queuing.
/// </remarks>
/// <param name="actionService">Heals the releases, fixes the cross-references and queues the refreshes.</param>
public sealed class DownloadMissingMetadataForFilesAction(ActionService actionService) : IScheduledAction
{
    public string Name => "Download Missing Metadata for Files";

    public string? Description => "Heal the releases linking an episode without its series, fix the file cross-references missing their anime, and queue the metadata of every source that files are linked to but that is missing.";

    public ActionCategory Category => ActionCategory.Import;

    public IReadOnlyList<ActionTrigger> DefaultTriggers => [ActionTrigger.AtStartup];

    public Task Execute(IProgress<decimal> progress, CancellationToken token)
        => actionService.ScheduleMissingMetadataForFiles(progress, token);
}
