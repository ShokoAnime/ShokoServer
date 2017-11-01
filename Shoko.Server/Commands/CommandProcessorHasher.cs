using System;
using System.ComponentModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Force.DeepCloner;
using NLog;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands
{
    public class CommandProcessorHasher
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
                    Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

                    paused = value;
                    if (paused)
                    {
                        QueueState =
                            new QueueStateStruct {queueState = QueueStateEnum.Paused, extraParams = new string[0]};
                        pauseTime = DateTime.Now;
                    }
                    else
                    {
                        QueueState =
                            new QueueStateStruct {queueState = QueueStateEnum.Idle, extraParams = new string[0]};
                        pauseTime = null;
                    }

                    ServerInfo.Instance.HasherQueuePaused = paused;
                    ServerInfo.Instance.HasherQueueRunning = !paused;
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
                }
                Task.Factory.StartNew(() => OnQueueCountChangedEvent?.Invoke(new QueueCountEventArgs(value)));
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
                    return queueState.DeepClone();
                }
            }
            set
            {
                lock (lockQueueState)
                {
                    queueState = value.DeepClone();
                }
                Task.Factory.StartNew(() => OnQueueStateChangedEvent?.Invoke(new QueueStateEventArgs(value)));
            }
        }

        public bool ProcessingCommands => processingCommands;

        public bool IsWorkerBusy => workerCommands.IsBusy;

        public CommandProcessorHasher()
        {
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
            QueueCount = RepoFactory.CommandRequest.GetQueuedCommandCountHasher();

            if (QueueCount > 0) workerCommands.RunWorkerAsync();
        }

        public void Init()
        {
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

            processingCommands = true;
            QueueState = new QueueStateStruct
            {
                queueState = QueueStateEnum.StartingHasher,
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
            QueueCount = RepoFactory.CommandRequest.GetQueuedCommandCountHasher();
            // if the worker is busy, it will pick up the next command from the DB
            // do not pick new command if cancellation is requested
            if (processingCommands || workerCommands.CancellationPending)
                return;

            // otherwise need to start the worker again
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
                        TimeSpan ts = DateTime.Now - pauseTime.Value;
                        if (ts.TotalHours >= 6)
                            Paused = false;
                    }
                    catch
                    {
                        // ignore
                    }
                    Thread.Sleep(200);
                    continue;
                }

                CommandRequest crdb = RepoFactory.CommandRequest.GetNextDBCommandRequestHasher();
                if (crdb == null)
                {
                    if (QueueCount > 0)
                        logger.Error($"No command returned from repo, but there are {QueueCount} commands left");
                    return;
                }

                if (workerCommands.CancellationPending)
                {
                    e.Cancel = true;
                    return;
                }

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

                icr.ProcessCommand();

                RepoFactory.CommandRequest.Delete(crdb.CommandRequestID);
                QueueCount = RepoFactory.CommandRequest.GetQueuedCommandCountHasher();
            }
        }
    }
}