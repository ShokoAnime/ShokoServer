using System;
using System.ComponentModel;
using System.Globalization;
using System.Threading;
using JMMServer.Properties;
using JMMServer.Repositories;
using NLog;

namespace JMMServer.Commands
{
    public class CommandProcessorHasher
    {
        public delegate void QueueCountChangedHandler(QueueCountEventArgs ev);

        public delegate void QueueStateChangedHandler(QueueStateEventArgs ev);

        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private readonly object lockPaused = new object();

        private readonly object lockQueueCount = new object();
        private readonly object lockQueueState = new object();

        private bool paused;
        private DateTime? pauseTime;

        private int queueCount;

        private string queueState = Resources.Command_Idle;
        private readonly BackgroundWorker workerCommands = new BackgroundWorker();

        public CommandProcessorHasher()
        {
            workerCommands.WorkerReportsProgress = true;
            workerCommands.WorkerSupportsCancellation = true;
            workerCommands.DoWork += workerCommands_DoWork;
            workerCommands.RunWorkerCompleted += workerCommands_RunWorkerCompleted;
        }

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
                        QueueState = Resources.Command_Paused;
                        pauseTime = DateTime.Now;
                    }
                    else
                    {
                        QueueState = Resources.Command_Idle;
                        pauseTime = null;
                    }

                    ServerInfo.Instance.HasherQueuePaused = paused;
                    ServerInfo.Instance.HasherQueueRunning = !paused;
                }
            }
        }

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

        public string QueueState
        {
            get
            {
                lock (lockQueueState)
                {
                    Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

                    queueState = Resources.Command_Idle;
                    return queueState;
                }
            }
            set
            {
                lock (lockQueueCount)
                {
                    queueState = value;
                    OnQueueStateChangedEvent(new QueueStateEventArgs(queueState));
                }
            }
        }

        public bool ProcessingCommands { get; private set; }

        public event QueueCountChangedHandler OnQueueCountChangedEvent;

        protected void OQueueCountChanged(QueueCountEventArgs ev)
        {
            if (OnQueueCountChangedEvent != null)
            {
                OnQueueCountChangedEvent(ev);
            }
        }

        public event QueueStateChangedHandler OnQueueStateChangedEvent;

        protected void OQueueStateChanged(QueueStateEventArgs ev)
        {
            if (OnQueueStateChangedEvent != null)
            {
                OnQueueStateChangedEvent(ev);
            }
        }

        private void workerCommands_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

            ProcessingCommands = false;
            //logger.Trace("Stopping command worker (hasher)...");
            QueueState = Resources.Command_Idle;
            QueueCount = 0;
        }

        public void Init()
        {
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

            ProcessingCommands = true;
            //logger.Trace("Starting command worker (hasher)...");
            QueueState = Resources.Command_StartingHasher;
            workerCommands.RunWorkerAsync();
        }

        public void Stop()
        {
            workerCommands.CancelAsync();
        }

        /// <summary>
        ///     This is simply used to tell the command processor that a new command has been added to the database
        /// </summary>
        public void NotifyOfNewCommand()
        {
            // if the worker is busy, it will pick up the next command from the DB
            if (ProcessingCommands)
            {
                //logger.Trace("NotifyOfNewCommand (hasher) exiting, worker already busy");
                return;
            }

            // otherwise need to start the worker again
            //logger.Trace("Restarting command worker (hasher)...");

            ProcessingCommands = true;
            if (!workerCommands.IsBusy)
                workerCommands.RunWorkerAsync();
        }

        private void workerCommands_DoWork(object sender, DoWorkEventArgs e)
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
                        var ts = DateTime.Now - pauseTime.Value;
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

                //logger.Trace("Looking for next command request (hasher)...");

                var repCR = new CommandRequestRepository();
                var crdb = repCR.GetNextDBCommandRequestHasher();
                if (crdb == null) return;

                if (workerCommands.CancellationPending)
                {
                    e.Cancel = true;
                    return;
                }

                QueueCount = repCR.GetQueuedCommandCountHasher();
                //logger.Trace("{0} commands remaining in queue (hasher)", QueueCount);

                //logger.Trace("Next command request (hasher): {0}", crdb.CommandID);

                var icr = CommandHelper.GetCommand(crdb);
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
                repCR.Delete(crdb.CommandRequestID);
                QueueCount = repCR.GetQueuedCommandCountHasher();
            }
        }
    }
}