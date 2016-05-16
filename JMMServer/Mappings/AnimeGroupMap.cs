using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentNHibernate.Mapping;
using JMMServer.Entities;

namespace JMMServer.Mappings
{
	public class AnimeGroupMap : ClassMap<AnimeGroup>
	{
		public AnimeGroupMap()
        {
			Not.LazyLoad();
            Id(x => x.AnimeGroupID);

			Map(x => x.AnimeGroupParentID);
			Map(x => x.DefaultAnimeSeriesID);
			Map(x => x.DateTimeCreated).Not.Nullable();
			Map(x => x.DateTimeUpdated).Not.Nullable();
			Map(x => x.Description);
			Map(x => x.GroupName);
			Map(x => x.IsManuallyNamed).Not.Nullable();
			Map(x => x.OverrideDescription).Not.Nullable();
			Map(x => x.SortName);
			Map(x => x.EpisodeAddedDate);
			Map(x => x.MissingEpisodeCount).Not.Nullable();
			Map(x => x.MissingEpisodeCountGroups).Not.Nullable();
            Map(x => x.ContractVersion).Not.Nullable();
            Map(x => x.ContractString).Nullable().CustomType("StringClob");

        }
    }
}
