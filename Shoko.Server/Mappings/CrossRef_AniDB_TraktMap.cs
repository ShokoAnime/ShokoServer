using FluentNHibernate.Mapping;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Obsolete;

namespace Shoko.Server.Mappings
{
#pragma warning disable CS0612 // Type or member is obsolete
    public class CrossRef_AniDB_TraktMap : ClassMap<CrossRef_AniDB_Trakt>
#pragma warning restore CS0612 // Type or member is obsolete
    {
        public CrossRef_AniDB_TraktMap()
        {
            Table("CrossRef_AniDB_Trakt");

            Not.LazyLoad();
            Id(x => x.CrossRef_AniDB_TraktID);

            Map(x => x.AnimeID).Not.Nullable();
            Map(x => x.CrossRefSource).Not.Nullable();
            Map(x => x.TraktID);
            Map(x => x.TraktSeasonNumber).Not.Nullable();
        }
    }
}