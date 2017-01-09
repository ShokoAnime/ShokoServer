using FluentNHibernate.Mapping;
using JMMServer.Entities;
using Shoko.Models.Server;

namespace JMMServer.Mappings
{
    public class ScanFileMap : ClassMap<ScanFile>
    {
        public ScanFileMap()
        {
            Not.LazyLoad();
            Id(x => x.ScanFileID);
            Map(x => x.ScanID).Not.Nullable();
            Map(x => x.ImportFolderID).Not.Nullable();
            Map(x => x.VideoLocal_Place_ID).Not.Nullable();
            Map(x => x.FullName).Not.Nullable();
            Map(x => x.FileSize).Not.Nullable();
            Map(x => x.Status).Not.Nullable();
            Map(x => x.CheckDate).Nullable();
            Map(x => x.Hash).Not.Nullable();
            Map(x => x.HashResult).Nullable();
        }
    }
}
