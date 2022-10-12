﻿using FluentNHibernate.Mapping;
using Shoko.Models.Server;

namespace Shoko.Server.Mappings;

public class AniDB_Anime_DefaultImageMap : ClassMap<AniDB_Anime_DefaultImage>
{
    public AniDB_Anime_DefaultImageMap()
    {
        Table("AniDB_Anime_DefaultImage");
        Not.LazyLoad();
        Id(x => x.AniDB_Anime_DefaultImageID);

        Map(x => x.AnimeID).Not.Nullable();
        Map(x => x.ImageParentID).Not.Nullable();
        Map(x => x.ImageParentType).Not.Nullable();
        Map(x => x.ImageType).Not.Nullable();
    }
}
