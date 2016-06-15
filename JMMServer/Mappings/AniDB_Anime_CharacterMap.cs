using FluentNHibernate.Mapping;
using JMMServer.Entities;

namespace JMMServer.Mappings
{
    public class AniDB_Anime_CharacterMap : ClassMap<AniDB_Anime_Character>
    {
        public AniDB_Anime_CharacterMap()
        {
            Not.LazyLoad();
            Id(x => x.AniDB_Anime_CharacterID);

            Map(x => x.AnimeID).Not.Nullable();
            Map(x => x.CharID).Not.Nullable();
            Map(x => x.CharType).Not.Nullable();
            Map(x => x.EpisodeListRaw);
        }
    }
}