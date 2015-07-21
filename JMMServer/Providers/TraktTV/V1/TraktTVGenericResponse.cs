using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using JMMServer.Entities;
using NLog;

namespace JMMServer.Providers.TraktTV
{
	[DataContract]
	public class TraktTVGenericResponse
	{
		private static Logger logger = LogManager.GetCurrentClassLogger();

		[DataMember]
		public string status { get; set; }

		[DataMember]
		public string message { get; set; }

		[DataMember]
		public string error { get; set; }

		public bool IsSuccess
		{
			get
			{
				if (string.IsNullOrEmpty(status)) return false;

				return (status.Equals("success", StringComparison.InvariantCultureIgnoreCase));
			}
		}

		public TraktTVGenericResponse()
		{
		}
	}
}
