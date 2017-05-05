using System;
using System.ComponentModel;
using System.Globalization;
using System.Threading;
using Shoko.Server.Repositories.Direct;
using NLog;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands
{
    public class CommandProcessorGeneral
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private BackgroundWorker workerCommands = new BackgroundWorker();
        private bool processingCommands = false;
        private DateTime? pauseTime = null;


        private object lockQueueCount = new object();
        private object lockQueueState = new object();
        private object lockPaused = new object();

        public delegate void QueueCountChangedHandler(QueueCountEventArgs ev);

        public event QueueCountChangedHandler OnQueueCountChangedEvent;

        public delegate void QueueStateChangedHandler(QueueStateEventArgs ev);

        public event QueueStateChangedHandler OnQueueStateChangedEvent;

        private bool paused = false;

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
                        ShokoService.AnidbProcessor.BanOrigin = "";
                    }
                    ServerInfo.Instance.GeneralQueuePaused = paused;
                    ServerInfo.Instance.GeneralQueueRunning = !paused;
                }
            }
        }

        private int queueCount = 0;

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
                    OnQueueCountChangedEvent(new QueueCountEventArgs(queueCount));
                }
            }
        }

        private QueueStateStruct queueState =
            new QueueStateStruct() {queueState = QueueStateEnum.Idle, extraParams = new string[0]};

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
                    OnQueueStateChangedEvent(new QueueStateEventArgs(queueState));
                }
            }
        }

        public bool ProcessingCommands
        {
            get { return processingCommands; }
        }

        public CommandProcessorGeneral()
        {
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

            workerCommands.WorkerReportsProgress = true;
            workerCommands.WorkerSupportsCancellation = true;
            workerCommands.DoWork += new DoWorkEventHandler(workerCommands_DoWork);
            workerCommands.RunWorkerCompleted += new RunWorkerCompletedEventHandler(workerCommands_RunWorkerCompleted);
        }

        void workerCommands_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

            processingCommands = false;
            //logger.Trace("Stopping command worker...");
            QueueState = new QueueStateStruct() {queueState = QueueStateEnum.Idle, extraParams = new string[0]};

            QueueCount = 0;
        }

        public void Init()
        {
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

            processingCommands = true;
            //logger.Trace("Starting command worker...");
            QueueState = new QueueStateStruct()
            {
                queueState = QueueStateEnum.StartingGeneral,
                extraParams = new string[0]
            };
            this.workerCommands.RunWorkerAsync();
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
            // if the worker is busy, it will pick up the next command from the DB
            // do not pick new command if cancellation is requested
            if (processingCommands || workerCommands.CancellationPending)
            {
                //logger.Trace("NotifyOfNewCommand exiting, worker already busy");
                return;
            }

            // otherwise need to start the worker again
            //logger.Trace("Restarting command worker...");

            processingCommands = true;
            if (!workerCommands.IsBusy)
                this.workerCommands.RunWorkerAsync();
        }

        void workerCommands_DoWork(object sender, DoWorkEventArgs e)
        {
            while (true)
            {
                if (workerCommands.CancellationPending)
                {
                    e.Cancel = true;
                    return;
                }

                // if paused we will sleep for 5 seconds, and the try again
                // we will remove the pause if it was set more than 6 hours ago
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

                        //logger.Trace("Queue is paused: {0}", pauseTime.Value);
                        TimeSpan ts = DateTime.Now - pauseTime.Value;
                        if (ts.TotalHours >= 6)
                        {
                            Paused = false;
                        }
                    }
                    catch
                    {
                    }
                    Thread.Sleep(5000);
                    continue;
                }


                //logger.Trace("Looking for next command request...");

                CommandRequest crdb = RepoFactory.CommandRequest.GetNextDBCommandRequestGeneral();
                if (crdb == null) return;

                if (workerCommands.CancellationPending)
                {
                    e.Cancel = true;
                    return;
                }

                QueueCount = RepoFactory.CommandRequest.GetQueuedCommandCountGeneral();
                //logger.Trace("{0} commands remaining in queue", QueueCount);

                //logger.Trace("Next command request: {0}", crdb.CommandID);

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
                        logger.Error(ex, "ProcessCommand exception: {0}\n{1}", crdb.CommandID, ex.ToString());
                    }
                }

                logger.Trace("Deleting command request: {0}", crdb.CommandID);
                RepoFactory.CommandRequest.Delete(crdb.CommandRequestID);

                QueueCount = RepoFactory.CommandRequest.GetQueuedCommandCountGeneral();
            }
        }
    }
}