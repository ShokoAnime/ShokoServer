using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentNHibernate.Mapping;
using JMMServer.Entities;

namespace JMMServer.Mappings
{
	public class DuplicateFileMap : ClassMap<DuplicateFile>
	{
		public DuplicateFileMap()
        {
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
