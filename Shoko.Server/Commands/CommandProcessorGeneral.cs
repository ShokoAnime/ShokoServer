using System;
using System.ComponentModel;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Repositories;
using Shoko.Server.Server;

namespace Shoko.Server.Commands
{
    public class CommandProcessorGeneral : CommandProcessor
    {
        public override void Init(IServiceProvider provider)
        {
            base.Init(provider);
            QueueState = new QueueStateStruct
            {
                message = "Starting general command worker",
                queueState = QueueStateEnum.StartingGeneral,
                extraParams = new string[0]
            };
        }

        protected override void WorkerCommands_DoWork(object sender, DoWorkEventArgs e)
        {
            var udpHandler = ServiceProvider.GetRequiredService<IUDPConnectionHandler>();
            var httpHandler = ServiceProvider.GetRequiredService<IHttpConnectionHandler>();
            while (true)
            {
                if (WorkerCommands.CancellationPending)
                    return;

                // if paused we will sleep for 5 seconds, and the try again
                if (Paused)
                {
                    try
                    {
                        if (WorkerCommands.CancellationPending)
                            return;
                    }
                    catch
                    {
                        // ignore
                    }
                    Thread.Sleep(200);
                    continue;
                }

                CommandRequest crdb = RepoFactory.CommandRequest.GetNextDBCommandRequestGeneral(udpHandler, httpHandler);
                if (crdb == null)
                {
                    if (QueueCount > 0 && !httpHandler.IsBanned && !udpHandler.IsBanned)
                        Logger.LogError($"No command returned from database, but there are {QueueCount} commands left");
                    return;
                }

                if (WorkerCommands.CancellationPending)
                    return;

                ICommandRequest icr = CommandHelper.GetCommand(ServiceProvider, crdb);
                if (icr == null)
                {
                    Logger.LogError("No implementation found for command: {0}-{1}", crdb.CommandType, crdb.CommandID);
                }
                else
                {
                    QueueState = icr.PrettyDescription;

                    if (WorkerCommands.CancellationPending)
                        return;

                    Logger.LogTrace("Processing command request: {0}", crdb.CommandID);
                    try
                    {
                        CurrentCommand = crdb;
                        icr.ProcessCommand(ServiceProvider);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "ProcessCommand exception: {0}\n{1}", crdb.CommandID, ex);
                    }
                    finally
                    {
                        CurrentCommand = null;
                    }
                }

                Logger.LogTrace("Deleting command request: {0}", crdb.CommandID);
                RepoFactory.CommandRequest.Delete(crdb.CommandRequestID);

                UpdateQueueCount();
            }
        }

        protected override void UpdateQueueCount()
        {
            QueueCount = RepoFactory.CommandRequest.GetQueuedCommandCountGeneral();
        }

        protected override string QueueType { get; } = "General";

        protected override void UpdatePause(bool pauseState)
        {
            ServerInfo.Instance.GeneralQueuePaused = pauseState;
            ServerInfo.Instance.GeneralQueueRunning = !pauseState;
        }
    }
}
