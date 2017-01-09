using FluentNHibernate.Mapping;
using JMMServer.Entities;
using Shoko.Models.Server;

namespace JMMServer.Mappings
{
    public class FileFfdshowPresetMap : ClassMap<FileFfdshowPreset>
    {
        public FileFfdshowPresetMap()
        {
            Not.LazyLoad();
            Id(x => x.FileFfdshowPresetID);

            Map(x => x.Hash);
            Map(x => x.FileSize).Not.Nullable();
            Map(x => x.Preset);
        }
    }
}