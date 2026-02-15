using FluentNHibernate.Mapping;
using Shoko.Server.Models.Legacy;
using Shoko.Server.Server;

namespace Shoko.Server.Mappings;

public class ScanFileMap : ClassMap<ScanFile>
{
    public ScanFileMap()
    {
        Table("ScanFile");

        Not.LazyLoad();
        Id(x => x.ScanFileID);
        Map(x => x.ScanID).Not.Nullable();
        Map(x => x.ImportFolderID).Not.Nullable();
        Map(x => x.VideoLocal_Place_ID).Not.Nullable();
        Map(x => x.FullName).Not.Nullable();
        Map(x => x.FileSize).Not.Nullable();
        Map(x => x.Status).Not.Nullable().CustomType<ScanFileStatus>();
        Map(x => x.CheckDate).Nullable();
        Map(x => x.Hash).Not.Nullable();
        Map(x => x.HashResult).Nullable();
    }
}
