using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using JMMContracts;


namespace JMMServer.Providers.TraktTV
{
	[DataContract]
	public class TraktTVFriendRequest
	{
		public TraktTVFriendRequest() { }

		[DataMember]
		public string username { get; set; }

		[DataMember]
		public string full_name { get; set; }

		[DataMember]
		public string gender { get; set; }

		[DataMember]
		public string age { get; set; }

		[DataMember]
		public string location { get; set; }

		[DataMember]
		public string about { get; set; }

		[DataMember]
		public int joined { get; set; }

		[DataMember]
		public string avatar { get; set; }

		[DataMember]
		public string url { get; set; }

		[DataMember]
		public int requested { get; set; }

		public override string ToString()
		{
			return string.Format("{0}", username);
		}

		public Contract_Trakt_FriendFrequest ToContract()
		{
			Contract_Trakt_FriendFrequest contract = new Contract_Trakt_FriendFrequest();

			contract.Username = username;
			contract.Full_name = full_name;
			contract.Gender = gender;
			contract.Age = age;
			contract.Location = location;
			contract.About = about;
			contract.Joined = joined;
			contract.Requested = requested;
			contract.Avatar = avatar;
			contract.Url = url;
			contract.RequestedDate = Utils.GetAniDBDateAsDate(requested);
			contract.JoinedDate = Utils.GetAniDBDateAsDate(joined);

			return contract;
		}
	}
}
