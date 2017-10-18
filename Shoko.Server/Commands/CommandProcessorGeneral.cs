using System;
using System.ComponentModel;
using System.Globalization;
using System.Threading;
using NLog;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands
{
    public class CommandProcessorGeneral
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private readonly BackgroundWorker workerCommands = new BackgroundWorker();
        private bool processingCommands;
        private DateTime? pauseTime;


        private readonly object lockQueueCount = new object();
        private readonly object lockQueueState = new object();
        private readonly object lockPaused = new object();

        public delegate void QueueCountChangedHandler(QueueCountEventArgs ev);

        public event QueueCountChangedHandler OnQueueCountChangedEvent;

        public delegate void QueueStateChangedHandler(QueueStateEventArgs ev);

        public event QueueStateChangedHandler OnQueueStateChangedEvent;

        private bool paused;

        public bool Paused
        {
            get
            {
                lock (lockPaused)
                {
                    return paused;
                }
            }
            set
            {
                lock (lockPaused)
                {
                    paused = value;
                    if (paused)
                    {
                        QueueState = new QueueStateStruct
                        {
                            queueState = QueueStateEnum.Paused,
                            extraParams = new string[0]
                        };
                        pauseTime = DateTime.Now;
                    }
                    else
                    {
                        QueueState = new QueueStateStruct
                        {
                            queueState = QueueStateEnum.Idle,
                            extraParams = new string[0]
                        };
                        pauseTime = null;
                        ShokoService.AnidbProcessor.IsBanned = false;
                        ShokoService.AnidbProcessor.BanOrigin = string.Empty;
                    }
                    ServerInfo.Instance.GeneralQueuePaused = paused;
                    ServerInfo.Instance.GeneralQueueRunning = !paused;
                }
            }
        }

        private int queueCount;

        public int QueueCount
        {
            get
            {
                lock (lockQueueCount)
                {
                    return queueCount;
                }
            }
            set
            {
                lock (lockQueueCount)
                {
                    queueCount = value;
                    OnQueueCountChangedEvent?.Invoke(new QueueCountEventArgs(queueCount));
                }
            }
        }

        private QueueStateStruct queueState =
            new QueueStateStruct {queueState = QueueStateEnum.Idle, extraParams = new string[0]};

        public QueueStateStruct QueueState
        {
            get
            {
                lock (lockQueueState)
                {
                    return queueState;
                }
            }
            set
            {
                lock (lockQueueState)
                {
                    queueState = value;
                    OnQueueStateChangedEvent?.Invoke(new QueueStateEventArgs(queueState));
                }
            }
        }

        public bool ProcessingCommands => processingCommands;

        public CommandProcessorGeneral()
        {
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

            workerCommands.WorkerReportsProgress = true;
            workerCommands.WorkerSupportsCancellation = true;
            workerCommands.DoWork += WorkerCommands_DoWork;
            workerCommands.RunWorkerCompleted += WorkerCommands_RunWorkerCompleted;
        }

        void WorkerCommands_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

            processingCommands = false;
            paused = false;

            QueueState = new QueueStateStruct {queueState = QueueStateEnum.Idle, extraParams = new string[0]};

            QueueCount = 0;
        }

        public void Init()
        {
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

            processingCommands = true;
            QueueState = new QueueStateStruct
            {
                queueState = QueueStateEnum.StartingGeneral,
                extraParams = new string[0]
            };
            workerCommands.RunWorkerAsync();
        }

        public void Stop()
        {
            workerCommands.CancelAsync();
        }

        /// <summary>
        /// This is simply used to tell the command processor that a new command has been added to the database
        /// </summary>
        public void NotifyOfNewCommand()
        {
            QueueCount = RepoFactory.CommandRequest.GetQueuedCommandCountGeneral();
            // if the worker is busy, it will pick up the next command from the DB
            // do not pick new command if cancellation is requested
            if (processingCommands || workerCommands.CancellationPending)
                return;

            processingCommands = true;
            if (!workerCommands.IsBusy)
                workerCommands.RunWorkerAsync();
        }

        void WorkerCommands_DoWork(object sender, DoWorkEventArgs e)
        {
            while (true)
            {
                if (workerCommands.CancellationPending)
                {
                    e.Cancel = true;
                    return;
                }

                // if paused we will sleep for 5 seconds, and the try again
                // we will remove the pause if it was set more than 12 hours ago
                // the pause is initiated when banned from AniDB or manually by the user
                if (Paused)
                {
                    try
                    {
                        if (workerCommands.CancellationPending)
                        {
                            e.Cancel = true;
                            return;
                        }

                        TimeSpan ts = DateTime.Now - pauseTime.Value;
                        if (ts.TotalHours >= 12)
                            Paused = false;
                    }
                    catch
                    {
                        // ignore
                    }
                    Thread.Sleep(200);
                    continue;
                }

                CommandRequest crdb = RepoFactory.CommandRequest.GetNextDBCommandRequestGeneral();
                if (crdb == null) return;

                if (workerCommands.CancellationPending)
                {
                    e.Cancel = true;
                    return;
                }

                ICommandRequest icr = CommandHelper.GetCommand(crdb);
                if (icr == null)
                {
                    logger.Error("No implementation found for command: {0}-{1}", crdb.CommandType, crdb.CommandID);
                }
                else
                {
                    QueueState = icr.PrettyDescription;

                    if (workerCommands.CancellationPending)
                    {
                        e.Cancel = true;
                        return;
                    }

                    logger.Trace("Processing command request: {0}", crdb.CommandID);
                    try
                    {
                        icr.ProcessCommand();
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, "ProcessCommand exception: {0}\n{1}", crdb.CommandID, ex);
                    }
                }

                logger.Trace("Deleting command request: {0}", crdb.CommandID);
                RepoFactory.CommandRequest.Delete(crdb.CommandRequestID);

                QueueCount = RepoFactory.CommandRequest.GetQueuedCommandCountGeneral();
            }
        }
    }
}