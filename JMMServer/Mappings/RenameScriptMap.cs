using FluentNHibernate.Mapping;
using JMMServer.Entities;

namespace JMMServer.Mappings
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
        }
    }
}