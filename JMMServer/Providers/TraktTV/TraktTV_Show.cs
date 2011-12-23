using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using JMMContracts;

namespace JMMServer.Providers.TraktTV
{
	[DataContract]
	public class TraktTV_Show
	{
		public TraktTV_Show() { }

		[DataMember]
		public string title { get; set; }

		[DataMember]
		public string year { get; set; }

		[DataMember]
		public string url { get; set; }

		[DataMember]
		public string first_aired { get; set; }

		[DataMember]
		public string country { get; set; }

		[DataMember]
		public string overview { get; set; }

		[DataMember]
		public string tvdb_id { get; set; }

		[DataMember]
		public TraktTVImages images { get; set; }

		[DataMember]
		public string imdb_id { get; set; }

		[DataMember]
		public string tvrage_id { get; set; }

		[DataMember]
		public int runtime { get; set; }

		[DataMember]
		public string network { get; set; }

		[DataMember]
		public string air_day { get; set; }

		[DataMember]
		public string air_time { get; set; }

		[DataMember]
		public string certification { get; set; }

		[DataMember]
		public TraktTV_Ratings ratings { get; set; }


		public string TraktID
		{
			get
			{
				if (string.IsNullOrEmpty(url)) return "";

				int pos = url.LastIndexOf("/");
				if (pos < 0) return "";

				string id = url.Substring(pos + 1, url.Length - pos - 1);
				return id;
			}
		}

		public override string ToString()
		{
			return string.Format("{0} - {1} - {2}", title, year, overview);
		}

		public Contract_TraktTVShowResponse ToContract()
		{
			Contract_TraktTVShowResponse contract = new Contract_TraktTVShowResponse();

			contract.title = title;
			contract.year = year;
			contract.url = url;
			contract.first_aired = first_aired;
			contract.country = country;
			contract.overview = overview;
			contract.tvdb_id = tvdb_id;

			return contract;
		}
	}
}
