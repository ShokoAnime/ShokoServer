using FluentNHibernate.Mapping;
using Shoko.Models.Server;

namespace Shoko.Server.Mappings
{
    public class RenameScriptMap : ClassMap<RenameScript>
    {
        public RenameScriptMap()
        {
            Not.LazyLoad();
            Id(x => x.RenameScriptID);

            Map(x => x.ScriptName);
            Map(x => x.Script);
            Map(x => x.IsEnabledOnImport).Not.Nullable();
            Map(x => x.RenamerType).Not.Nullable().Default("Legacy");
            Map(x => x.ExtraData).Nullable();
        }
    }
}