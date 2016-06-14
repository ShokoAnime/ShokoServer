using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentNHibernate.Mapping;
using JMMServer.Entities;

namespace JMMServer.Mappings
{
	public class AniDB_EpisodeMap : ClassMap<AniDB_Episode>
	{
		public AniDB_EpisodeMap()
        {
			Not.LazyLoad();
            Id(x => x.AniDB_EpisodeID);

			Map(x => x.AirDate).Not.Nullable();
			Map(x => x.AnimeID).Not.Nullable();
			Map(x => x.DateTimeUpdated).Not.Nullable();
			Map(x => x.EnglishName).Not.Nullable();
			Map(x => x.EpisodeID).Not.Nullable();
			Map(x => x.EpisodeNumber).Not.Nullable();
			Map(x => x.EpisodeType).Not.Nullable();
			Map(x => x.LengthSeconds).Not.Nullable();
			Map(x => x.Rating).Not.Nullable();
			Map(x => x.RomajiName).Not.Nullable();
			Map(x => x.Votes).Not.Nullable();
        }
	}
}
