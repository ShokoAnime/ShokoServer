using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentNHibernate.Mapping;
using JMMServer.Entities;

namespace JMMServer.Mappings
{
	public class AnimeGroup_UserMap : ClassMap<AnimeGroup_User>
	{
		public AnimeGroup_UserMap()
        {
			Not.LazyLoad();
            Id(x => x.AnimeGroup_UserID);

			Map(x => x.JMMUserID);
			Map(x => x.AnimeGroupID);
			Map(x => x.IsFave).Not.Nullable();
			Map(x => x.PlayedCount).Not.Nullable();
			Map(x => x.StoppedCount).Not.Nullable();
			Map(x => x.UnwatchedEpisodeCount).Not.Nullable();
			Map(x => x.WatchedCount).Not.Nullable();
			Map(x => x.WatchedDate);
			Map(x => x.WatchedEpisodeCount);
            Map(x => x.PlexContractVersion).Not.Nullable();
            Map(x => x.PlexContractString).Nullable().CustomType("StringClob");
            Map(x => x.KodiContractVersion).Not.Nullable();
            Map(x => x.KodiContractString).Nullable().CustomType("StringClob");
        }
	}
}
