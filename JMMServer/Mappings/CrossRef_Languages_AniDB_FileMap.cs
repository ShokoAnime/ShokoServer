using FluentNHibernate.Mapping;
using JMMServer.Entities;
using Shoko.Models.Server;

namespace JMMServer.Mappings
{
    public class CrossRef_Languages_AniDB_FileMap : ClassMap<CrossRef_Languages_AniDB_File>
    {
        public CrossRef_Languages_AniDB_FileMap()
        {
            Not.LazyLoad();
            Id(x => x.CrossRef_Languages_AniDB_FileID);

            Map(x => x.FileID).Not.Nullable();
            Map(x => x.LanguageID).Not.Nullable();
        }
    }
}