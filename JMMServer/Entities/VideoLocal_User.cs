using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AniDBAPI;
using JMMServer.Repositories;
using JMMContracts;
using System.IO;
using JMMServer.Commands;
using NLog;
using BinaryNorthwest;

namespace JMMServer.Entities
{
	public class VideoLocal_User
	{
		private static Logger logger = LogManager.GetCurrentClassLogger();

		public int VideoLocal_UserID { get; private set; }
		public int JMMUserID { get; set; }
		public int VideoLocalID { get; set; }
		public DateTime WatchedDate { get; set; }
		
	}
}
