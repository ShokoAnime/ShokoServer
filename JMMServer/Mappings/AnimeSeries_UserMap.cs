using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentNHibernate.Mapping;
using JMMServer.Entities;

namespace JMMServer.Mappings
{
	public class AnimeSeries_UserMap : ClassMap<AnimeSeries_User>
	{
		public AnimeSeries_UserMap()
        {
			Not.LazyLoad();
            Id(x => x.AnimeSeries_UserID);

			Map(x => x.JMMUserID).Not.Nullable();
			Map(x => x.AnimeSeriesID).Not.Nullable();
			Map(x => x.PlayedCount).Not.Nullable();
			Map(x => x.StoppedCount).Not.Nullable();
			Map(x => x.UnwatchedEpisodeCount).Not.Nullable();
			Map(x => x.WatchedCount).Not.Nullable();
			Map(x => x.WatchedDate);
			Map(x => x.WatchedEpisodeCount).Not.Nullable();
            Map(x => x.PlexContractVersion).Not.Nullable();
            Map(x => x.PlexContractString).Nullable().CustomType("StringClob");
            Map(x => x.KodiContractVersion).Not.Nullable();
            Map(x => x.KodiContractString).Nullable().CustomType("StringClob");
        }
	}
}
