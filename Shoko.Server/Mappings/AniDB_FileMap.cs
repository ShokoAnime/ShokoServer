using FluentNHibernate.Mapping;
using Shoko.Server.Models;

namespace Shoko.Server.Mappings
{
    public class AniDB_FileMap : ClassMap<SVR_AniDB_File>
    {
        public AniDB_FileMap()
        {
            Table("AniDB_File");
            Not.LazyLoad();
            Id(x => x.AniDB_FileID);

            Map(x => x.AnimeID).Not.Nullable();
            Map(x => x.DateTimeUpdated).Not.Nullable();
            Map(x => x.File_Description).Not.Nullable();
            Map(x => x.File_ReleaseDate).Not.Nullable();
            Map(x => x.File_Source).Not.Nullable();
            Map(x => x.FileID).Not.Nullable();
            Map(x => x.FileName).Not.Nullable();
            Map(x => x.FileSize).Not.Nullable();
            Map(x => x.FileVersion).Not.Nullable();
            Map(x => x.IsCensored).Nullable();
            Map(x => x.IsDeprecated).Not.Nullable();
            Map(x => x.IsChaptered).Not.Nullable();
            Map(x => x.InternalVersion).Not.Nullable();
            Map(x => x.GroupID).Not.Nullable();
            Map(x => x.Hash).Not.Nullable();
        }
    }
}