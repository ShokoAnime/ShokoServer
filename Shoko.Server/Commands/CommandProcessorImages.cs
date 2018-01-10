using System;
using System.ComponentModel;
using System.Globalization;
using System.Threading;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands
{
    public class CommandProcessorImages : IDisposable
    {
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

                    ServerInfo.Instance.ImagesQueuePaused = paused;
                    ServerInfo.Instance.ImagesQueueRunning = !paused;
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

        public CommandProcessorImages()
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
            QueueCount = 0;
        }

        public void Init()
        {
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

            processingCommands = true;

            QueueState = new QueueStateStruct
            {
                queueState = QueueStateEnum.StartingImages,
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
            QueueCount = Repo.CommandRequest.GetQueuedCommandCountImages();
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

                Shoko.Models.Server.CommandRequest crdb = Repo.CommandRequest.GetNextDBCommandRequestImages();
                if (crdb == null) return;

                if (workerCommands.CancellationPending)
                {
                    e.Cancel = true;
                    return;
                }

                CommandRequest icr = CommandHelper.GetCommand(crdb);
                if (icr == null)
                    return;

                if (workerCommands.CancellationPending)
                {
                    e.Cancel = true;
                    return;
                }

                QueueState = icr.PrettyDescription;

                icr.ProcessCommand();

                Repo.CommandRequest.Delete(crdb.CommandRequestID);
                QueueCount = Repo.CommandRequest.GetQueuedCommandCountImages();
            }
        }

        public void Dispose()
        {
            workerCommands.Dispose();
        }
    }
}