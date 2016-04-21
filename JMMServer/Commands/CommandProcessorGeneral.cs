using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using NLog;
using JMMServer.Repositories;
using JMMServer.Entities;
using System.Threading;
using System.Collections;
using System.Collections.Specialized;
using System.Configuration;
using System.Globalization;

namespace JMMServer.Commands
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
		protected void OQueueCountChanged(QueueCountEventArgs ev)
		{
			if (OnQueueCountChangedEvent != null)
			{
				OnQueueCountChangedEvent(ev);
			}
		}

		public delegate void QueueStateChangedHandler(QueueStateEventArgs ev);
		public event QueueStateChangedHandler OnQueueStateChangedEvent;
		protected void OQueueStateChanged(QueueStateEventArgs ev)
		{
			if (OnQueueStateChangedEvent != null)
			{  
                OnQueueStateChangedEvent(ev);
			}
		}

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
						QueueState = JMMServer.Properties.Resources.Command_Paused;
						pauseTime = DateTime.Now;
					}
					else
					{
                        QueueState = JMMServer.Properties.Resources.Command_Idle;
                        pauseTime = null;
						JMMService.AnidbProcessor.IsBanned = false;
						JMMService.AnidbProcessor.BanOrigin = "";
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

		private string queueState = JMMServer.Properties.Resources.Command_Idle;
        public string QueueState
		{
			get
			{
				lock (lockQueueState)
				{
                    Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

                    queueState = JMMServer.Properties.Resources.Command_Idle;
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
			workerCommands.RunWorkerCompleted +=new RunWorkerCompletedEventHandler(workerCommands_RunWorkerCompleted);
        }

		void  workerCommands_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

            processingCommands = false;
			//logger.Trace("Stopping command worker...");
			QueueState = JMMServer.Properties.Resources.Command_Idle;
			QueueCount = 0;
		}

		public void Init()
		{
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

            processingCommands = true;
			//logger.Trace("Starting command worker...");
		    QueueState = JMMServer.Properties.Resources.Command_StartingGeneral;
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
			if (processingCommands)
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
					catch { }
					Thread.Sleep(5000);
					continue;
				}


				//logger.Trace("Looking for next command request...");

				CommandRequestRepository repCR = new CommandRequestRepository();
				CommandRequest crdb = repCR.GetNextDBCommandRequestGeneral();
				if (crdb == null) return;

				if (workerCommands.CancellationPending)
				{
					e.Cancel = true;
					return;
				}

				QueueCount = repCR.GetQueuedCommandCountGeneral();
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
				    icr.ProcessCommand();
                }

				logger.Trace("Deleting command request: {0}", crdb.CommandID);
				repCR.Delete(crdb.CommandRequestID);

				QueueCount = repCR.GetQueuedCommandCountGeneral();
			}
		}
	}

	
}
