using FluentNHibernate.Mapping;
using JMMServer.Entities;
using Shoko.Models.Server;

namespace JMMServer.Mappings
{
    public class AniDB_Anime_CharacterMap : ClassMap<SVR_AniDB_Anime_Character>
    {
        public AniDB_Anime_CharacterMap()
        {
            Table("AniDB_Anime_Character");
            Not.LazyLoad();
            Id(x => x.AniDB_Anime_CharacterID);
            Map(x => x.AnimeID).Not.Nullable();
            Map(x => x.CharID).Not.Nullable();
            Map(x => x.CharType).Not.Nullable();
            Map(x => x.EpisodeListRaw);
        }
    }
}