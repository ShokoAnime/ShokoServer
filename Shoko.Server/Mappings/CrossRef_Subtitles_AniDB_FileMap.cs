using FluentNHibernate.Mapping;
using Shoko.Server.Models;
using Shoko.Models.Server;

namespace Shoko.Server.Mappings
{
    public class CrossRef_Subtitles_AniDB_FileMap : ClassMap<CrossRef_Subtitles_AniDB_File>
    {
        public CrossRef_Subtitles_AniDB_FileMap()
        {
            Not.LazyLoad();
            Id(x => x.CrossRef_Subtitles_AniDB_FileID);

            Map(x => x.FileID).Not.Nullable();
            Map(x => x.LanguageID).Not.Nullable();
        }
    }
}