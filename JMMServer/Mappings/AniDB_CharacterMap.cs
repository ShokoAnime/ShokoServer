using FluentNHibernate.Mapping;
using JMMServer.Entities;

namespace JMMServer.Mappings
{
    public class AniDB_CharacterMap : ClassMap<AniDB_Character>
    {
        public AniDB_CharacterMap()
        {
            Not.LazyLoad();
            Id(x => x.AniDB_CharacterID);

            Map(x => x.CharDescription).Not.Nullable().CustomType("StringClob");
            Map(x => x.CharID).Not.Nullable();
            Map(x => x.PicName).Not.Nullable();
            Map(x => x.CharKanjiName).Not.Nullable();
            Map(x => x.CharName).Not.Nullable();
            Map(x => x.CreatorListRaw).Not.Nullable();
        }
    }
}