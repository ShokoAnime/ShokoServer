using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMMServer.Commands
{
	public class QueueStateEventArgs : EventArgs
	{
		public readonly string QueueState;

		public QueueStateEventArgs(string queueState)
		{
			QueueState = queueState;
		}
	}
}
