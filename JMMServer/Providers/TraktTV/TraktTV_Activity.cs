using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

namespace JMMServer.Providers.TraktTV
{
	[DataContract]
	public class TraktTV_Activity
	{
		public TraktTV_Activity() { }

		[DataMember]
		public int timestamp { get; set; }

		[DataMember]
		public TraktTV_When when { get; set; }

		[DataMember]
		public TraktTV_Elapsed elapsed { get; set; }

		[DataMember]
		public string type { get; set; }

		[DataMember]
		public string action { get; set; }

		[DataMember]
		public TraktTV_UserActivity user { get; set; }

		[DataMember]
		public TraktTV_Shout shout { get; set; }

		[DataMember]
		public TraktTV_Episode episode { get; set; }

		[DataMember]
		public TraktTV_Show show { get; set; }

		public int ActivityAction
		{
			get
			{
				if (action.Equals("scrobble", StringComparison.InvariantCultureIgnoreCase)) return 1;
				if (action.Equals("shout", StringComparison.InvariantCultureIgnoreCase)) return 2;
				return 0;
			}
		}

		public int ActivityType
		{
			get
			{
				if (type.Equals("episode", StringComparison.InvariantCultureIgnoreCase)) return 1;
				if (type.Equals("show", StringComparison.InvariantCultureIgnoreCase)) return 2;
				return 0;
			}
		}

		public override string ToString()
		{
			return string.Format("{0}/{1} - {2}", ActivityAction, ActivityType, Utils.GetAniDBDateAsDate(timestamp));
		}
	}
}
