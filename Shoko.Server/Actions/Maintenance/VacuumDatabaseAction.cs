using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.ScheduledActions;
using Shoko.QueueProcessor;
using Shoko.Server.Databases;
using Shoko.Server.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Gives back the space deleted rows left behind in the database, which an
///   upgrade that drops tables leaves unused until then.
/// </summary>
/// <remarks>
///   SQLite holds its write lock for the whole vacuum, and every other writer
///   gives up after its busy timeout, so on SQLite the database is blocked and
///   the queue paused and drained first, and both are released afterwards.
/// </remarks>
public sealed class VacuumDatabaseAction(
    DatabaseFactory databaseFactory,
    SystemService systemService,
    QueueHandler queueHandler,
    ILogger<VacuumDatabaseAction> logger
) : IScheduledAction
{
    /// <summary>
    ///   How long to wait for the jobs already running to finish before
    ///   vacuuming anyway.
    /// </summary>
    private static readonly TimeSpan _drainTimeout = TimeSpan.FromMinutes(5);

    public string Name => "Vacuum Database";

    public string? Description => "Give back the space deleted rows left behind. SQLite rebuilds its database file, which needs free disk space of up to twice " +
        "the file's size; the queue is paused and the API refuses requests until it is done, so run it while the server is idle. MySQL and MariaDB " +
        "optimize every table; SQL Server reuses the space itself, so nothing is done there.";

    public ActionCategory Category => ActionCategory.Maintenance;

    public bool RequiresConfirmation => true;

    public string? ConfirmationMessage => "On SQLite the queue is paused and the API refuses requests until the vacuum is done. Vacuum the database now?";

    public async Task Execute(IProgress<decimal> progress, CancellationToken token)
    {
        var database = databaseFactory.Instance!;
        if (database is not SQLite)
        {
            await Task.Run(database.Vacuum, token);
            return;
        }

        var paused = queueHandler.Paused;
        var released = new TaskCompletionSource();
        systemService.AddDatabaseBlockingTask(released.Task);
        try
        {
            if (!paused)
                await queueHandler.Pause();

            // This action runs in a queue job of its own, which is the one left
            // once the others are done.
            var deadline = DateTime.UtcNow + _drainTimeout;
            while (queueHandler.GetExecutingJobs().Length > 1)
            {
                if (DateTime.UtcNow >= deadline)
                {
                    logger.LogWarning("Vacuuming the database while other jobs are still running; their writes may fail and be retried");
                    break;
                }

                await Task.Delay(TimeSpan.FromSeconds(1), token);
            }

            await Task.Run(database.Vacuum, token);
        }
        finally
        {
            if (!paused)
                await queueHandler.Resume();

            // A failed vacuum leaves the database as it was, so the block is
            // lifted either way.
            released.SetResult();
        }
    }
}
