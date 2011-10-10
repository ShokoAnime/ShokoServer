using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using AniDBAPI;
using JMMServer.Entities;

namespace JMMServer.WebCache
{
	[Serializable]
	[XmlRoot("DeleteCrossRef_AniDBTvDBAll_Request")]
	public class DeleteCrossRef_AniDBTvDBAll_Request : XMLBase
	{
		protected int seriesID = 0;
		public int SeriesID
		{
			get { return seriesID; }
			set { seriesID = value; }
		}

		// default constructor
		public DeleteCrossRef_AniDBTvDBAll_Request()
		{
		}

		// default constructor
		public DeleteCrossRef_AniDBTvDBAll_Request(int tvDBID)
		{
			this.SeriesID = tvDBID;
		}
	}
}
