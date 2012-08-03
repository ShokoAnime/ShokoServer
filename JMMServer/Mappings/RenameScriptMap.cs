using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Entities;
using FluentNHibernate.Mapping;

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
