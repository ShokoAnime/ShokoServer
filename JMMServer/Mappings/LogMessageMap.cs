using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentNHibernate.Mapping;
using JMMServer.Entities;

namespace JMMServer.Mappings
{
	public class LogMessageMap : ClassMap<LogMessage>
	{
		public LogMessageMap()
        {
			Not.LazyLoad();
			Id(x => x.LogMessageID);

			Map(x => x.LogType);
			Map(x => x.LogContent).Not.Nullable();
			Map(x => x.LogDate).Not.Nullable();
        }
	}
}
