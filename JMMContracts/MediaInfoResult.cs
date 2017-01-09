using Shoko.Models.PlexAndKodi;

namespace Shoko.Models
{
    public class MediaInfoResult
    {
        public string VideoCodec { get; set; }
        public string VideoBitrate { get; set; }
        public string VideoBitDepth { get; set; }
        public string VideoFrameRate { get; set; }
        public string VideoResolution { get; set; }
        public string AudioCodec { get; set; }
        public string AudioBitrate { get; set; }
        public int Duration { get; set; }
        public Media Media { get; set; }
    }
}