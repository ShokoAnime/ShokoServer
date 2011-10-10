using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentNHibernate.Mapping;
using JMMServer.Entities;


namespace JMMServer.Mappings
{
	public class Trakt_ImagePosterMap : ClassMap<Trakt_ImagePoster>
	{
		public Trakt_ImagePosterMap()
        {
			Not.LazyLoad();
            Id(x => x.Trakt_ImagePosterID);

			Map(x => x.Enabled).Not.Nullable();
			Map(x => x.ImageURL);
			Map(x => x.Season).Not.Nullable();
			Map(x => x.Trakt_ShowID).Not.Nullable();
        }
	}
}
