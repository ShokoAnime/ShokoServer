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

		public bool ProcessingCommands
		{
			get { return processingCommands; }
		}

		public CommandProcessorHasher()
        {
			workerCommands.WorkerReportsProgress = true;
			workerCommands.WorkerSupportsCancellation = true;
			workerCommands.DoWork += new DoWorkEventHandler(workerCommands_DoWork);
			workerCommands.RunWorkerCompleted +=new RunWorkerCompletedEventHandler(workerCommands_RunWorkerCompleted);
        }

		void  workerCommands_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			processingCommands = false;
			logger.Trace("Stopping command worker (hasher)...");
			QueueState = "Idle";
			QueueCount = 0;
		}

		public void Init()
		{
			processingCommands = true;
			logger.Trace("Starting command worker (hasher)...");
			QueueState = "Starting command worker (hasher)...";
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
				//logger.Trace("NotifyOfNewCommand (hasher) exiting, worker already busy");
				return;
			}
			
			// otherwise need to start the worker again
			logger.Trace("Restarting command worker (hasher)...");

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

						logger.Trace("Hasher Queue is paused: {0}", pauseTime.Value);
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

				logger.Trace("Looking for next command request (hasher)...");

				CommandRequestRepository repCR = new CommandRequestRepository();
				CommandRequest crdb = repCR.GetNextDBCommandRequestHasher();
				if (crdb == null) return;

				if (workerCommands.CancellationPending)
				{
					e.Cancel = true;
					return;
				}

				QueueCount = repCR.GetQueuedCommandCountHasher();
				logger.Trace("{0} commands remaining in queue (hasher)", QueueCount);

				logger.Trace("Next command request (hasher): {0}", crdb.CommandID);

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

				logger.Trace("Processing command request (hasher): {0}", crdb.CommandID);
				icr.ProcessCommand();

				logger.Trace("Deleting command request (hasher): {0}", crdb.CommandID);
				repCR.Delete(crdb.CommandRequestID);
				QueueCount = repCR.GetQueuedCommandCountHasher();
			}
		}
	}
}
