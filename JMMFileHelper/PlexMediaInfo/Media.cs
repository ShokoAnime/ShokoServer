using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace PlexMediaInfo
{
    [XmlType("Media")]
    [Serializable]
    public class Media
    {
        [XmlElement("Part")]
        public List<Part> Parts { get; set; }

        [XmlAttribute("duration")]
        public string Duration { get; set; }

        [XmlAttribute("videoFrameRate")]
        public string VideoFrameRate { get; set; }

        [XmlAttribute("container")]
        public string Container { get; set; }

        [XmlAttribute("videoCodec")]
        public string VideoCodec { get; set; }

        [XmlAttribute("audioCodec")]
        public string AudioCodec { get; set; }

        [XmlAttribute("audioChannels")]
        public string AudioChannels { get; set; }

        [XmlAttribute("aspectRatio")]
        public string AspectRatio { get; set; }

        [XmlAttribute("height")]
        public string Height { get; set; }

        [XmlAttribute("width")]
        public string Width { get; set; }


        [XmlAttribute("bitrate")]
        public string Bitrate { get; set; }

        [XmlAttribute("id")]
        public string Id { get; set; }

        [XmlAttribute("videoResolution")]
        public string VideoResolution { get; set; }

        [XmlAttribute("optimizedForStreaming")]
        public string OptimizedForStreaming { get; set; }
    }
    [XmlType("Part")]
    public class Part 
    {
        [XmlElement("Stream")]
        public List<Stream> Streams { get; set; }

        [XmlAttribute("size")]
        public string Size { get; set; }

        [XmlAttribute("duration")]
        public string Duration { get; set; }

        [XmlAttribute("key")]
        public string Key { get; set; }

        [XmlAttribute("container")]
        public string Container { get; set; }

        [XmlAttribute("id")]
        public string Id { get; set; }

        [XmlAttribute("file")]
        public string File { get; set; }
 
        [XmlAttribute("optimizedForStreaming")]
        public string OptimizedForStreaming { get; set; }


        [XmlAttribute("has64bitOffsets")]
        public string Has64bitOffsets { get; set; }
    }

    [XmlType("Stream")]
    public class Stream
    {

        [XmlAttribute("language")]
        public string Language { get; set; }

        [XmlAttribute("key")]
        public string Key { get; set; }


        [XmlAttribute("duration")]
        public string Duration { get; set; }

        [XmlAttribute("height")]
        public string Height { get; set; }

        [XmlAttribute("width")]
        public string Width { get; set; }

        [XmlAttribute("bitrate")]
        public string Bitrate { get; set; }

        [XmlAttribute("id")]
        public string Id { get; set; }

        [XmlAttribute("scanType")]
        public string ScanType { get; set; }
        [XmlAttribute("refFrames")]
        public string RefFrames { get; set; }
        [XmlAttribute("profile")]
        public string Profile { get; set; }

        [XmlAttribute("level")]
        public string Level { get; set; }

        [XmlAttribute("headerStripping")]
        public string HeaderStripping { get; set; }

        [XmlAttribute("hasScalingMatrix")]
        public string HasScalingMatrix { get; set; }

        [XmlAttribute("frameRateMode")]
        public string FrameRateMode { get; set; }


        [XmlAttribute("frameRate")]
        public string FrameRate { get; set; }

        [XmlAttribute("colorSpace")]
        public string ColorSpace { get; set; }

        [XmlAttribute("chromaSubsampling")]
        public string ChromaSubsampling { get; set; }

        [XmlAttribute("cabac")]
        public string Cabac { get; set; }


        [XmlAttribute("bitDepth")]
        public string BitDepth { get; set; }

        [XmlAttribute("index")]
        public string Index { get; set; }

        [XmlIgnore] internal int idx;

        [XmlAttribute("codec")]
        public string Codec { get; set; }

        [XmlAttribute("streamType")]
        public string StreamType { get; set; }

        [XmlAttribute("orientation")]
        public string Orientation { get; set; }

        [XmlAttribute("qpel")]
        public string QPel { get; set; }

        [XmlAttribute("gmc")]
        public string GMC { get; set; }

        [XmlAttribute("bvop")]
        public string BVOP { get; set; }

        [XmlAttribute("samplingRate")]
        public string SamplingRate { get; set; }
        [XmlAttribute("languageCode")]
        public string LanguageCode { get; set; }

        [XmlAttribute("channels")]
        public string Channels { get; set; }


        [XmlAttribute("selected")]
        public string Selected { get; set; }

        [XmlAttribute("dialogNorm")]
        public string DialogNorm { get; set; }


        [XmlAttribute("bitrateMode")]
        public string BitrateMode { get; set; }


        [XmlAttribute("format")]
        public string Format { get; set; }
        [XmlAttribute("default")]
        public string Default { get; set; }

        [XmlAttribute("forced")]
        public string Forced { get; set; }

        [XmlAttribute("pixelAspectRatio")]
        public string PixelAspectRatio { get; set; }

        [XmlIgnore]
        internal float PA { get; set; }
    }
}
