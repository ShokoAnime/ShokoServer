using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMMServer.Commands
{
	public class QueueCountEventArgs : EventArgs
	{
		public readonly int QueueCount;

		public QueueCountEventArgs(int queueCount)
		{
			QueueCount = queueCount;
		}
	}
}
