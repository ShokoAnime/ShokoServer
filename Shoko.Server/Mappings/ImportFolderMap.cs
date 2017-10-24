using FluentNHibernate.Mapping;
using Shoko.Server.Models;

namespace Shoko.Server.Mappings
{
    public class ImportFolderMap : ClassMap<SVR_ImportFolder>
    {
        public ImportFolderMap()
        {
            Table("ImportFolder");
            Not.LazyLoad();
            Id(x => x.ImportFolderID);

            Map(x => x.ImportFolderType).Not.Nullable();
            Map(x => x.ImportFolderLocation).Not.Nullable();
            Map(x => x.ImportFolderName).Not.Nullable();
            Map(x => x.IsDropDestination).Not.Nullable();
            Map(x => x.IsDropSource).Not.Nullable();
            Map(x => x.CloudID).Nullable();
            Map(x => x.IsWatched).Not.Nullable();
        }
    }
}