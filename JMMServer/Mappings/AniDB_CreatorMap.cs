using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentNHibernate.Mapping;
using JMMServer.Entities;

namespace JMMServer.Mappings
{
	public class AniDB_CreatorMap : ClassMap<AniDB_Creator>
	{
		public AniDB_CreatorMap()
        {
			Not.LazyLoad();
            Id(x => x.AniDB_CreatorID);

			Map(x => x.CreatorDescription);
			Map(x => x.CreatorID).Not.Nullable();
			Map(x => x.CreatorKanjiName);
			Map(x => x.CreatorName).Not.Nullable();
			Map(x => x.CreatorType).Not.Nullable();
			Map(x => x.PicName);
			Map(x => x.URLEnglish);
			Map(x => x.URLJapanese);
			Map(x => x.URLWikiEnglish);
			Map(x => x.URLWikiJapanese);
        }
	}
}
