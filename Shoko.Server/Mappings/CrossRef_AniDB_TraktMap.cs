using FluentNHibernate.Mapping;
using Shoko.Models.Server;
using Shoko.Server.Entities;

namespace Shoko.Server.Mappings
{
    public class CrossRef_AniDB_TraktMap : ClassMap<SVR_CrossRef_AniDB_Trakt>
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