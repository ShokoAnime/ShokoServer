using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using JMMServer.Entities;
using NLog;
using JMMServer.Repositories;

namespace JMMServer.Providers.TraktTV
{
	[DataContract]
	public class TraktTVSeasonEpisode
	{
		private static Logger logger = LogManager.GetCurrentClassLogger();

		[DataMember]
		public string season { get; set; }

		[DataMember]
		public string episode { get; set; }
	}
}
