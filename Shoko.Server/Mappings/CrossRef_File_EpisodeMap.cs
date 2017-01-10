using FluentNHibernate.Mapping;
using Shoko.Models.Server;
using Shoko.Server.Entities;

namespace Shoko.Server.Mappings
{
    public class CrossRef_File_EpisodeMap : ClassMap<SVR_CrossRef_File_Episode>
    {
        public CrossRef_File_EpisodeMap()
        {
            Table("CrossRef_File_Episode");

            Not.LazyLoad();
            Id(x => x.CrossRef_File_EpisodeID);

            Map(x => x.CrossRefSource).Not.Nullable();
            Map(x => x.EpisodeID).Not.Nullable();
            Map(x => x.EpisodeOrder).Not.Nullable();
            Map(x => x.Hash).Not.Nullable();
            Map(x => x.Percentage).Not.Nullable();
            Map(x => x.FileName).Not.Nullable();
            Map(x => x.FileSize).Not.Nullable();
            Map(x => x.AnimeID).Not.Nullable();
        }
    }
}