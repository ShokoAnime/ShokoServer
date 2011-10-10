using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMMServer.Entities
{
	public class VideoInfo
	{
		public int VideoInfoID { get; private set; }
		public string Hash { get; set; }
		public long FileSize { get; set; }
		public string FileName { get; set; }
		public DateTime DateTimeUpdated { get; set; }
		public string VideoCodec { get; set; }
		public string VideoBitrate { get; set; }
		public string VideoFrameRate { get; set; }
		public string VideoResolution { get; set; }
		public string AudioCodec { get; set; }
		public string AudioBitrate { get; set; }
		public long Duration { get; set; }
	}
}
