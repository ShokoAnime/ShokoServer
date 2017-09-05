using System;
using System.ComponentModel;
using System.Globalization;
using System.Threading;
using Shoko.Server.Repositories.Direct;
using NLog;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands
{
    public class CommandProcessorHasher
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
                    Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

                    paused = value;
                    if (paused)
                    {
                        QueueState =
                            new QueueStateStruct() {queueState = QueueStateEnum.Paused, extraParams = new string[0]};
                        pauseTime = DateTime.Now;
                    }
                    else
                    {
                        QueueState =
                            new QueueStateStruct() {queueState = QueueStateEnum.Idle, extraParams = new string[0]};
                        pauseTime = null;
                    }

                    ServerInfo.Instance.HasherQueuePaused = paused;
                    ServerInfo.Instance.HasherQueueRunning = !paused;
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
                }
                OnQueueCountChangedEvent(new QueueCountEventArgs(queueCount));
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
                }
                OnQueueStateChangedEvent(new QueueStateEventArgs(queueState));
            }
        }

        public bool ProcessingCommands
        {
            get { return processingCommands; }
        }

        public CommandProcessorHasher()
        {
            workerCommands.WorkerReportsProgress = true;
            workerCommands.WorkerSupportsCancellation = true;
            workerCommands.DoWork += new DoWorkEventHandler(WorkerCommands_DoWork);
            workerCommands.RunWorkerCompleted += new RunWorkerCompletedEventHandler(WorkerCommands_RunWorkerCompleted);
        }

        void WorkerCommands_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

            processingCommands = false;
            paused = false;
            //logger.Trace("Stopping command worker (hasher)...");
            QueueState = new QueueStateStruct() {queueState = QueueStateEnum.Idle, extraParams = new string[0]};
            QueueCount = 0;
        }

        public void Init()
        {
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

            processingCommands = true;
            //logger.Trace("Starting command worker (hasher)...");
            QueueState = new QueueStateStruct()
            {
                queueState = QueueStateEnum.StartingHasher,
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
                //logger.Trace("NotifyOfNewCommand (hasher) exiting, worker already busy");
                return;
            }

            // otherwise need to start the worker again
            //logger.Trace("Restarting command worker (hasher)...");

            processingCommands = true;
            if (!workerCommands.IsBusy)
                this.workerCommands.RunWorkerAsync();
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

                        //logger.Trace("Hasher Queue is paused: {0}", pauseTime.Value);
                        TimeSpan ts = DateTime.Now - pauseTime.Value;
                        if (ts.TotalHours >= 6)
                        {
                            Paused = false;
                        }
                    }
                    catch
                    {
                    }
                    Thread.Sleep(200);
                    continue;
                }

                //logger.Trace("Looking for next command request (hasher)...");

                CommandRequest crdb = RepoFactory.CommandRequest.GetNextDBCommandRequestHasher();
                if (crdb == null) return;

                if (workerCommands.CancellationPending)
                {
                    e.Cancel = true;
                    return;
                }

                QueueCount = RepoFactory.CommandRequest.GetQueuedCommandCountHasher();
                //logger.Trace("{0} commands remaining in queue (hasher)", QueueCount);

                //logger.Trace("Next command request (hasher): {0}", crdb.CommandID);

                ICommandRequest icr = CommandHelper.GetCommand(crdb);
                if (icr == null)
                {
                    logger.Trace("No implementation found for command: {0}-{1}", crdb.CommandType, crdb.CommandID);
                    return;
                }

                QueueState = icr.PrettyDescription;

                if (workerCommands.CancellationPending)
                {
                    e.Cancel = true;
                    return;
                }

                //logger.Trace("Processing command request (hasher): {0}", crdb.CommandID);
                icr.ProcessCommand();

                //logger.Trace("Deleting command request (hasher): {0}", crdb.CommandID);
                RepoFactory.CommandRequest.Delete(crdb.CommandRequestID);
                QueueCount = RepoFactory.CommandRequest.GetQueuedCommandCountHasher();
            }
        }
    }
}