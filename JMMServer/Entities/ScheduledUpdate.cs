using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMMServer.Entities
{
	public class ScheduledUpdate
	{
		public int ScheduledUpdateID { get; private set; }
		public int UpdateType { get; set; }
		public DateTime LastUpdate { get; set; }
		public string UpdateDetails { get; set; }
	}
}
