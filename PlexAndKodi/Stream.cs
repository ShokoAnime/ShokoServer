using System;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using Nancy.Rest.Annotations.Atributes;

namespace Shoko.Models.PlexAndKodi
{
    [XmlType("Stream")]
    [DataContract]
    [Serializable]
    public class Stream
    {
        [DataMember(EmitDefaultValue = false, Order = 1)]
        [XmlAttribute("title")]
        public string Title { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 2)]
        [XmlAttribute("language")]
        public string Language { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 3)]
        [XmlAttribute("key")]
        public string Key { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 4)]
        [XmlAttribute("duration")]
        public string Duration { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 5)]
        [XmlAttribute("height")]
        public string Height { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 6)]
        [XmlAttribute("width")]
        public string Width { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 7)]
        [XmlAttribute("bitrate")]
        public string Bitrate { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 8)]
        [XmlAttribute("subIndex")]
        public string SubIndex { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 9)]
        [XmlAttribute("id")]
        public string Id { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 10)]
        [XmlAttribute("scanType")]
        public string ScanType { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 11)]
        [XmlAttribute("refFrames")]
        public string RefFrames { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 12)]
        [XmlAttribute("profile")]
        public string Profile { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 13)]
        [XmlAttribute("level")]
        public string Level { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 14)]
        [XmlAttribute("headerStripping")]
        public string HeaderStripping { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 15)]
        [XmlAttribute("hasScalingMatrix")]
        public string HasScalingMatrix { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 16)]
        [XmlAttribute("frameRateMode")]
        public string FrameRateMode { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 17)]
        [XmlAttribute("file")]
        public string File { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 18)]
        [XmlAttribute("frameRate")]
        public string FrameRate { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 19)]
        [XmlAttribute("colorSpace")]
        public string ColorSpace { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 20)]
        [XmlAttribute("codecID")]
        public string CodecID { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 21)]
        [XmlAttribute("chromaSubsampling")]
        public string ChromaSubsampling { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 22)]
        [XmlAttribute("cabac")]
        public string Cabac { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 23)]
        [XmlAttribute("bitDepth")]
        public string BitDepth { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 24)]
        [XmlAttribute("index")]
        public string Index { get; set; }

        [XmlIgnore]
        [Ignore]
        public int idx;

        [DataMember(EmitDefaultValue = false, Order = 25)]
        [XmlAttribute("codec")]
        public string Codec { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 26)]
        [XmlAttribute("streamType")]
        public string StreamType { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 27)]
        [XmlAttribute("orientation")]
        public string Orientation { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 28)]
        [XmlAttribute("qpel")]
        public string QPel { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 29)]
        [XmlAttribute("gmc")]
        public string GMC { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 30)]
        [XmlAttribute("bvop")]
        public string BVOP { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 31)]
        [XmlAttribute("samplingRate")]
        public string SamplingRate { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 32)]
        [XmlAttribute("languageCode")]
        public string LanguageCode { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 33)]
        [XmlAttribute("channels")]
        public string Channels { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 34)]
        [XmlAttribute("selected")]
        public string Selected { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 35)]
        [XmlAttribute("dialogNorm")]
        public string DialogNorm { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 36)]
        [XmlAttribute("bitrateMode")]
        public string BitrateMode { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 37)]
        [XmlAttribute("format")]
        public string Format { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 38)]
        [XmlAttribute("default")]
        public string Default { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 39)]
        [XmlAttribute("forced")]
        public string Forced { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 40)]
        [XmlAttribute("pixelAspectRatio")]
        public string PixelAspectRatio { get; set; }

        [XmlIgnore]
        [Ignore]
        public float PA { get; set; }
    }
}