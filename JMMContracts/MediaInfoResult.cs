namespace JMMContracts
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

        public string FullInfo { get; set; }
    }
}