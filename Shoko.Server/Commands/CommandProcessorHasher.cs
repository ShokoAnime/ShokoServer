using System;
using System.ComponentModel;
using System.Threading;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands;

public class CommandProcessorHasher : CommandProcessor
{
    protected override void UpdateQueueCount()
    {
        QueueCount = RepoFactory.CommandRequest.GetQueuedCommandCountHasher();
    }

    protected override string QueueType { get; } = "Hasher";

    protected override void UpdatePause(bool pauseState)
    {
        ServerInfo.Instance.HasherQueuePaused = pauseState;
        ServerInfo.Instance.HasherQueueRunning = !pauseState;
    }

    public override void Init(IServiceProvider provider)
    {
        base.Init(provider);
        QueueState = new QueueStateStruct
        {
            message = "Starting hasher command worker",
            queueState = QueueStateEnum.StartingHasher,
            extraParams = new string[0]
        };
    }

    protected override void WorkerCommands_DoWork(object sender, DoWorkEventArgs e)
    {
        while (true)
        {
            try
            {
                if (WorkerCommands.CancellationPending) return;

                // if paused we will sleep for 5 seconds, and the try again
                if (Paused)
                {
                    try
                    {
                        if (WorkerCommands.CancellationPending) return;
                    }
                    catch
                    {
                        // ignore
                    }

                    Thread.Sleep(200);
                    continue;
                }

                if (WorkerCommands.CancellationPending) return;

                var crdb = RepoFactory.CommandRequest.GetNextDBCommandRequestHasher();
                if (crdb == null)
                {
                    if (QueueCount > 0)
                        Logger.LogError("No command returned from repo, but there are {QueueCount} commands left",
                            QueueCount);

                    return;
                }

                var icr = CommandHelper.GetCommand(ServiceProvider, crdb);

                if (icr == null)
                {
                    Logger.LogTrace("No implementation found for command: {CommandType}-{CommandID}", crdb.CommandType,
                        crdb.CommandID);
                    return;
                }

                QueueState = icr.PrettyDescription;

                if (WorkerCommands.CancellationPending) return;

                try
                {
                    CurrentCommand = crdb;
                    icr.ProcessCommand();
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "ProcessCommand exception: {CommandID}\n{Ex}", crdb.CommandID, ex);
                    Logger.LogInformation(ex, "Removing ProcessCommand: {CommandID}", crdb.CommandID);
                    RepoFactory.CommandRequest.Delete(crdb.CommandRequestID);
                }
                finally
                {
                    CurrentCommand = null;
                }

                RepoFactory.CommandRequest.Delete(crdb.CommandRequestID);
                UpdateQueueCount();
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, "Error Processing Commands: {EX}", exception);
            }
        }
    }
}
