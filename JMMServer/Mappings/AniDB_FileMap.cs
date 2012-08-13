using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentNHibernate.Mapping;
using JMMServer.Entities;

namespace JMMServer.Mappings
{
	public class AniDB_FileMap : ClassMap<AniDB_File>
	{
		public AniDB_FileMap()
        {
			Not.LazyLoad();
            Id(x => x.AniDB_FileID);

			Map(x => x.Anime_GroupName).Not.Nullable();
			Map(x => x.Anime_GroupNameShort).Not.Nullable();
			Map(x => x.AnimeID).Not.Nullable();
			Map(x => x.CRC).Not.Nullable();
			Map(x => x.DateTimeUpdated).Not.Nullable();
			Map(x => x.Episode_Rating).Not.Nullable();
			Map(x => x.Episode_Votes).Not.Nullable();
			Map(x => x.File_AudioCodec).Not.Nullable();
			Map(x => x.File_Description).Not.Nullable();
			Map(x => x.File_FileExtension).Not.Nullable();
			Map(x => x.File_LengthSeconds).Not.Nullable();
			Map(x => x.File_ReleaseDate).Not.Nullable();
			Map(x => x.File_Source).Not.Nullable();
			Map(x => x.File_VideoCodec).Not.Nullable();
			Map(x => x.File_VideoResolution).Not.Nullable();
			Map(x => x.FileID).Not.Nullable();
			Map(x => x.FileName).Not.Nullable();
			Map(x => x.FileSize).Not.Nullable();
			Map(x => x.FileVersion).Not.Nullable();
			Map(x => x.IsCensored).Not.Nullable();
			Map(x => x.IsDeprecated).Not.Nullable();
			Map(x => x.InternalVersion).Not.Nullable();
			Map(x => x.GroupID).Not.Nullable();
			Map(x => x.Hash).Not.Nullable();
			Map(x => x.IsWatched).Not.Nullable();
			Map(x => x.MD5).Not.Nullable();
			Map(x => x.SHA1).Not.Nullable();
			Map(x => x.WatchedDate);
        }
	}
}
