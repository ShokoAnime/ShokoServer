using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentNHibernate.Mapping;
using JMMServer.Entities;

namespace JMMServer.Mappings
{
	public class VideoInfoMap : ClassMap<VideoInfo>
	{
		public VideoInfoMap()
        {
			Not.LazyLoad();
            Id(x => x.VideoInfoID);

			Map(x => x.AudioBitrate).Not.Nullable();
			Map(x => x.AudioCodec).Not.Nullable();
			Map(x => x.DateTimeUpdated).Not.Nullable();
			Map(x => x.Duration).Not.Nullable();
			Map(x => x.FileName).Not.Nullable();
			Map(x => x.FileSize).Not.Nullable();
			Map(x => x.Hash).Not.Nullable();
			Map(x => x.VideoBitrate).Not.Nullable();
			Map(x => x.VideoCodec).Not.Nullable();
			Map(x => x.VideoFrameRate).Not.Nullable();
			Map(x => x.VideoResolution).Not.Nullable();
        }
	}
}
