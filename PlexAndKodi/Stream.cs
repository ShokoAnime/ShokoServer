using System;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using Nancy.Rest.Annotations.Atributes;

namespace Shoko.Models.PlexAndKodi
{
    [XmlType("Stream")]
    [DataContract]
    [Serializable]
    public class Stream : ICloneable
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

        [DataMember(EmitDefaultValue = true, Order = 4)]
        [XmlAttribute("duration")]
        public long Duration { get; set; }

        [DataMember(EmitDefaultValue = true, Order = 5)]
        [XmlAttribute("height")]
        public int Height { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 6)]
        [XmlAttribute("width")]
        public int Width { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 7)]
        [XmlAttribute("bitrate")]
        public int Bitrate { get; set; }

        [DataMember(EmitDefaultValue = true, Order = 8)]
        [XmlAttribute("subIndex")]
        public int SubIndex { get; set; }

        [DataMember(EmitDefaultValue = true, Order = 9)]
        [XmlAttribute("id")]
        public int Id { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 10)]
        [XmlAttribute("scanType")]
        public string ScanType { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 11)]
        [XmlAttribute("refFrames")]
        public byte RefFrames { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 12)]
        [XmlAttribute("profile")]
        public string Profile { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 13)]
        [XmlAttribute("level")]
        public int Level { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 14)]
        [XmlAttribute("headerStripping")]
        public byte HeaderStripping { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 15)]
        [XmlAttribute("hasScalingMatrix")]
        public byte HasScalingMatrix { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 16)]
        [XmlAttribute("frameRateMode")]
        public string FrameRateMode { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 17)]
        [XmlAttribute("file")]
        public string File { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 18)]
        [XmlAttribute("frameRate")]
        public float FrameRate { get; set; }

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
        public byte Cabac { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 23)]
        [XmlAttribute("bitDepth")]
        public byte BitDepth { get; set; }

        [DataMember(EmitDefaultValue = true, Order = 24)]
        [XmlAttribute("index")]
        public byte Index { get; set; }

        [XmlIgnore]
        [Ignore]
        public byte idx;

        [DataMember(EmitDefaultValue = false, Order = 25)]
        [XmlAttribute("codec")]
        public string Codec { get; set; }

        [DataMember(EmitDefaultValue = true, Order = 26)]
        [XmlAttribute("streamType")]
        public byte StreamType { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 27)]
        [XmlAttribute("orientation")]
        public byte Orientation { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 28)]
        [XmlAttribute("qpel")]
        public byte QPel { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 29)]
        [XmlAttribute("gmc")]
        public string GMC { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 30)]
        [XmlAttribute("bvop")]
        public byte BVOP { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 31)]
        [XmlAttribute("samplingRate")]
        public int SamplingRate { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 32)]
        [XmlAttribute("languageCode")]
        public string LanguageCode { get; set; }

        [DataMember(EmitDefaultValue = true, Order = 33)]
        [XmlAttribute("channels")]
        public byte Channels { get; set; }

        [DataMember(EmitDefaultValue = true, Order = 34)]
        [XmlAttribute("selected")]
        public byte Selected { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 35)]
        [XmlAttribute("dialogNorm")]
        public string DialogNorm { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 36)]
        [XmlAttribute("bitrateMode")]
        public string BitrateMode { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 37)]
        [XmlAttribute("format")]
        public string Format { get; set; }

        [DataMember(EmitDefaultValue = true, Order = 38)]
        [XmlAttribute("default")]
        public byte Default { get; set; }

        [DataMember(EmitDefaultValue = true, Order = 39)]
        [XmlAttribute("forced")]
        public byte Forced { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 40)]
        [XmlAttribute("pixelAspectRatio")]
        public string PixelAspectRatio { get; set; }

        [XmlIgnore]
        [Ignore]
        public float PA { get; set; }

        public object Clone()
        {
            Stream newStream = new Stream
            {
                idx = idx,
                Title = Title,
                Language = Language,
                Key = Key,
                Duration = Duration,
                Height = Height,
                Width = Width,
                Bitrate = Bitrate,
                SubIndex = SubIndex,
                Id = Id,
                ScanType = ScanType,
                RefFrames = RefFrames,
                Profile = Profile,
                Level = Level,
                HeaderStripping = HeaderStripping,
                HasScalingMatrix = HasScalingMatrix,
                FrameRateMode = FrameRateMode,
                File = File,
                FrameRate = FrameRate,
                ColorSpace = ColorSpace,
                CodecID = CodecID,
                ChromaSubsampling = ChromaSubsampling,
                Cabac = Cabac,
                BitDepth = BitDepth,
                Index = Index,
                Codec = Codec,
                StreamType = StreamType,
                Orientation = Orientation,
                QPel = QPel,
                GMC = GMC,
                BVOP = BVOP,
                SamplingRate = SamplingRate,
                LanguageCode = LanguageCode,
                Channels = Channels,
                Selected = Selected,
                DialogNorm = DialogNorm,
                BitrateMode = BitrateMode,
                Format = Format,
                Default = Default,
                Forced = Forced,
                PixelAspectRatio = PixelAspectRatio,
                PA = PA
            };
            return newStream;
        }
    }
}