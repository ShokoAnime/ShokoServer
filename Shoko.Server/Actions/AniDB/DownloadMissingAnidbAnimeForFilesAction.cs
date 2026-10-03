using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.ScheduledActions;
using Shoko.Server.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Fix the file cross-references missing their anime, and queue the anime
///   that files are linked to but that are missing. Runs at start-up by
///   default.
/// </summary>
/// <remarks>
///   Only queues the anime refreshes, which run on their own; the progress covers
///   the queuing.
/// </remarks>
/// <param name="actionService">Fixes the cross-references and queues the anime.</param>
public sealed class DownloadMissingAnidbAnimeForFilesAction(ActionService actionService) : IScheduledAction
{
    public string Name => "Download Missing AniDB Anime for Files";

    public string? Description => "Fix the file cross-references missing their anime, and queue the anime that files are linked to but that are missing.";

    public ActionCategory Category => ActionCategory.AniDB;

    public IReadOnlyList<ActionTrigger> DefaultTriggers => [ActionTrigger.AtStartup];

    public Task Execute(IProgress<decimal> progress, CancellationToken token)
        => actionService.ScheduleMissingAnidbAnimeForFiles(progress, token);
}
