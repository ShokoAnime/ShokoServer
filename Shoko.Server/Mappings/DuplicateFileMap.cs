using FluentNHibernate.Mapping;
using Shoko.Models.Server;
using Shoko.Server.Entities;

namespace Shoko.Server.Mappings
{
    public class DuplicateFileMap : ClassMap<SVR_DuplicateFile>
    {
        public DuplicateFileMap()
        {
            Table("DuplicateFile");

            Not.LazyLoad();
            Id(x => x.DuplicateFileID);

            Map(x => x.DateTimeUpdated).Not.Nullable();
            Map(x => x.FilePathFile1);
            Map(x => x.FilePathFile2);
            Map(x => x.Hash);
            Map(x => x.ImportFolderIDFile1).Not.Nullable();
            Map(x => x.ImportFolderIDFile2).Not.Nullable();
        }
    }
}