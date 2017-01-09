using FluentNHibernate.Mapping;
using Shoko.Server.Entities;
using Shoko.Models.Server;

namespace Shoko.Server.Mappings
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