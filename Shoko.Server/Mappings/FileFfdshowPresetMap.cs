using FluentNHibernate.Mapping;
using Shoko.Models.Server;
using Shoko.Server.Models;

namespace Shoko.Server.Mappings
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