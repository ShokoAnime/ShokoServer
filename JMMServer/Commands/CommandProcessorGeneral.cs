using System;
using System.ComponentModel;
using System.Globalization;
using System.Threading;
using JMMServer.Properties;
using JMMServer.Repositories;
using NLog;

namespace JMMServer.Commands
{
    public class CommandProcessorGeneral
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

        public CommandProcessorGeneral()
        {
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

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
                        JMMService.AnidbProcessor.IsBanned = false;
                        JMMService.AnidbProcessor.BanOrigin = "";
                    }
                    ServerInfo.Instance.GeneralQueuePaused = paused;
                    ServerInfo.Instance.GeneralQueueRunning = !paused;
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
            //logger.Trace("Stopping command worker...");
            QueueState = Resources.Command_Idle;
            QueueCount = 0;
        }

        public void Init()
        {
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

            ProcessingCommands = true;
            //logger.Trace("Starting command worker...");
            QueueState = Resources.Command_StartingGeneral;
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
                //logger.Trace("NotifyOfNewCommand exiting, worker already busy");
                return;
            }

            // otherwise need to start the worker again
            //logger.Trace("Restarting command worker...");

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

                        //logger.Trace("Queue is paused: {0}", pauseTime.Value);
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


                //logger.Trace("Looking for next command request...");

                var repCR = new CommandRequestRepository();
                var crdb = repCR.GetNextDBCommandRequestGeneral();
                if (crdb == null) return;

                if (workerCommands.CancellationPending)
                {
                    e.Cancel = true;
                    return;
                }

                QueueCount = repCR.GetQueuedCommandCountGeneral();
                //logger.Trace("{0} commands remaining in queue", QueueCount);

                //logger.Trace("Next command request: {0}", crdb.CommandID);

                var icr = CommandHelper.GetCommand(crdb);
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
                    icr.ProcessCommand();
                }

                logger.Trace("Deleting command request: {0}", crdb.CommandID);
                repCR.Delete(crdb.CommandRequestID);

                QueueCount = repCR.GetQueuedCommandCountGeneral();
            }
        }
    }
}