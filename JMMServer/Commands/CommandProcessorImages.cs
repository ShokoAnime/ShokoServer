using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NLog;
using System.ComponentModel;
using JMMServer.Repositories;
using JMMServer.Entities;
using System.Threading;

namespace JMMServer.Commands
{
	public class CommandProcessorImages
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
					paused = value;
					if (paused)
					{
						QueueState = "Paused";
						pauseTime = DateTime.Now;
					}
					else
					{
						QueueState = "Idle";
						pauseTime = null;
					}

					ServerInfo.Instance.ImagesQueuePaused = paused;
					ServerInfo.Instance.ImagesQueueRunning = !paused;
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

		private string queueState = "Idle";
		public string QueueState
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
				lock (lockQueueCount)
				{
					queueState = value;
					OnQueueStateChangedEvent(new QueueStateEventArgs(queueState));
				}
			}
		}

		public CommandProcessorImages()
        {
			workerCommands.WorkerReportsProgress = true;
			workerCommands.WorkerSupportsCancellation = true;
			workerCommands.DoWork += new DoWorkEventHandler(workerCommands_DoWork);
			workerCommands.RunWorkerCompleted +=new RunWorkerCompletedEventHandler(workerCommands_RunWorkerCompleted);
        }

		void  workerCommands_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			processingCommands = false;
			logger.Trace("Stopping command worker (images)...");
			QueueState = "Idle";
			QueueCount = 0;
		}

		public void Init()
		{
			processingCommands = true;
			logger.Trace("Starting command worker (images)...");
			QueueState = "Starting command worker (images)...";
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
				logger.Trace("NotifyOfNewCommand (images) exiting, worker already busy");
				return;
			}
			
			// otherwise need to start the worker again
			logger.Trace("Restarting command worker (images)...");

			processingCommands = true;
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

						logger.Trace("Images Queue is paused: {0}", pauseTime.Value);
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

				logger.Trace("Looking for next command request (images)...");

				CommandRequestRepository repCR = new CommandRequestRepository();
				CommandRequest crdb = repCR.GetNextDBCommandRequestImages();
				if (crdb == null) return;

				QueueCount = repCR.GetQueuedCommandCountImages();
				logger.Trace("{0} commands remaining in queue (images)", QueueCount);

				if (workerCommands.CancellationPending)
				{
					e.Cancel = true;
					return;
				}

				logger.Trace("Next command request (images): {0}", crdb.CommandID);

				ICommandRequest icr = CommandHelper.GetCommand(crdb);
				if (icr == null)
				{
					logger.Trace("No implementation found for command: {0}-{1}", crdb.CommandType, crdb.CommandID);
					return;
				}

				if (workerCommands.CancellationPending)
				{
					e.Cancel = true;
					return;
				}

				QueueState = icr.PrettyDescription;

				logger.Trace("Processing command request (images): {0}", crdb.CommandID);
				icr.ProcessCommand();

				logger.Trace("Deleting command request (images): {0}", crdb.CommandID);
				repCR.Delete(crdb.CommandRequestID);
				QueueCount = repCR.GetQueuedCommandCountImages();
			}
		}
	}
}
