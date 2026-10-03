using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using Shoko.Abstractions.ScheduledActions;
using Shoko.Abstractions.ScheduledActions.Services;
using Shoko.Server.Actions;

namespace Shoko.Server.API;

/// <summary>
///   Queues scheduled actions for the legacy routes that once ran their work
///   directly, and for the import those routes ran.
/// </summary>
internal static class LegacyScheduledActions
{
    #region Fields

    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    ///   The scheduled actions an import queues, in the order the old import
    ///   ran them.
    /// </summary>
    internal static readonly IReadOnlyList<Func<IScheduledActionService, CancellationToken, Task<bool>>> ImportSteps =
    [
        Invoke<HashUnhashedFilesAction>,
        Invoke<ScanManagedFoldersAction>,
        Invoke<SearchForMetadataMatchesAction>,
        Invoke<PurgeExpiredOrphanedMetadataAction>,
        Invoke<DownloadAllImagesAction>,
        Invoke<CheckForPreviouslyIgnoredFilesAction>,
        Invoke<CheckAnidbFileUpdatesAction>,
    ];

    /// <summary>
    ///   The types of the scheduled actions an import queues, in the order of
    ///   <see cref="ImportSteps"/>.
    /// </summary>
    internal static readonly IReadOnlyList<Type> ImportActionTypes = [.. ImportSteps.Select(step => step.Method.GetGenericArguments()[0])];

    #endregion

    #region Invoking

    /// <summary>
    ///   Queues a run of each of the import's scheduled actions, in order. One
    ///   that cannot be queued is logged, and the rest are still queued.
    /// </summary>
    /// <param name="service">The scheduled actions.</param>
    /// <param name="token">Cancels the queuing.</param>
    /// <returns>How many of the actions were queued, or already were.</returns>
    internal static async Task<int> InvokeImport(IScheduledActionService service, CancellationToken token = default)
    {
        var queued = 0;
        foreach (var step in ImportSteps)
        {
            if (await step(service, token).ConfigureAwait(false))
                queued++;
        }

        return queued;
    }

    /// <summary>
    ///   Queues a run of one scheduled action, as an admin would by hand. A
    ///   refusal or a failure to queue is logged, not thrown.
    /// </summary>
    /// <typeparam name="TAction">The scheduled action type.</typeparam>
    /// <param name="service">The scheduled actions.</param>
    /// <param name="token">Cancels the queuing.</param>
    /// <exception cref="OperationCanceledException">The queuing was cancelled.</exception>
    /// <returns>Whether a run was queued, or already was.</returns>
    internal static async Task<bool> Invoke<TAction>(IScheduledActionService service, CancellationToken token = default)
        where TAction : class, IScheduledAction
    {
        try
        {
            if (service.GetScheduledAction<TAction>() is not { } action)
            {
                _logger.Warn($"Could not queue {typeof(TAction).Name}: it is not a registered scheduled action.");
                return false;
            }

            if (await service.InvokeAsync(action.ID, token).ConfigureAwait(false) is { } refusal)
            {
                _logger.Info($"Did not queue \"{action.Name}\": {refusal.Reason}");
                return false;
            }

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.Error(ex, $"Could not queue {typeof(TAction).Name}.");
            return false;
        }
    }

    #endregion
}
