using System;
using System.ComponentModel;
using System.Threading;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands
{
    public class CommandProcessorImages : CommandProcessor
    {
        protected override void UpdateQueueCount()
        {
            QueueCount = RepoFactory.CommandRequest.GetQueuedCommandCountImages();
        }

        protected override string QueueType { get; } = "Image";

        protected override void UpdatePause(bool pauseState)
        {
            ServerInfo.Instance.ImagesQueuePaused = pauseState;
            ServerInfo.Instance.ImagesQueueRunning = !pauseState;
        }

        public override void Init()
        {
            base.Init();
            QueueState = new QueueStateStruct
            {
                queueState = QueueStateEnum.StartingImages,
                extraParams = new string[0]
            };
        }

        protected override void WorkerCommands_DoWork(object sender, DoWorkEventArgs e)
        {
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

                CommandRequest crdb = RepoFactory.CommandRequest.GetNextDBCommandRequestImages();
                if (crdb == null) return;

                if (WorkerCommands.CancellationPending)
                    return;

                ICommandRequest icr = CommandHelper.GetCommand(crdb);
                if (icr == null)
                    return;

                if (WorkerCommands.CancellationPending)
                    return;

                QueueState = icr.PrettyDescription;

                try
                {
                    CurrentCommand = crdb;
                    icr.ProcessCommand();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "ProcessCommand exception: {0}\n{1}", crdb.CommandID, ex);
                }
                finally
                {
                    CurrentCommand = null;
                }

                RepoFactory.CommandRequest.Delete(crdb.CommandRequestID);
                UpdateQueueCount();
            }
        }
    }
}
