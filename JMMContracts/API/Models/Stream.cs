using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JMMContracts.PlexAndKodi;

namespace JMMContracts.API.Models
{
    /// <summary>
    /// Model that represends Part
    /// </summary>
    public class Stream
    {
        private string Default { get; set; }

        /// <summary>
        /// id
        /// </summary>
        public string id { get; set; }

        /// <summary>
        /// type of stream 1 video, 2 audio, 3 subs
        /// </summary>
        public string type { get; set; }

        /// <summary>
        /// audio/video/sub codec
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string codec { get; set; }

        /// <summary>
        /// width
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string width { get; set; }

        /// <summary>
        /// height
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string height { get; set; }

        /// <summary>
        /// duration
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string duration { get; set; }

        /// <summary>
        /// Numers of audio channels
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string channels { get; set; }

        /// <summary>
        /// Language of subtitles 
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string language { get; set; }


        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string bitrate { get; private set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string scantype { get; private set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string refframes { get; private set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string profile { get; private set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string level { get; private set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string frameratemode { get; private set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string languagecode { get; private set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string framerate { get; private set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string colorspace { get; private set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string chromasubsampling { get; private set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string codecid { get; private set; }        
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string cabac { get; private set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string bitdepth { get; private set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string index { get; private set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string samplingrate { get; private set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string bitratemode { get; private set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string title { get; private set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string format { get; set; }
        public string selected { get; set; }

        /// <summary>
        /// Parametraless constructor for XML Serialization
        /// </summary>
        public Stream()
        {

        }

        /// <summary>
        /// Contructor that create Medias out of video
        /// </summary>
        public Stream(JMMContracts.PlexAndKodi.Stream stream)
        {
            switch (stream.StreamType)
            {
                case "1":
                    //video
                    codec = stream.Codec;
                    width = stream.Width;
                    height = stream.Height;
                    duration = stream.Duration;
                    bitrate = stream.Bitrate;
                    scantype = stream.ScanType;
                    refframes = stream.RefFrames;
                    profile = stream.Profile;
                    level = stream.Level;
                    frameratemode = stream.FrameRateMode;
                    framerate = stream.FrameRate;
                    colorspace = stream.ColorSpace;
                    codecid = stream.CodecID;
                    chromasubsampling = stream.ChromaSubsampling;
                    cabac = stream.Cabac;
                    bitdepth = stream.BitDepth;
                    index = stream.Index;
                    languagecode = stream.LanguageCode;
                    break;
                case "2":
                    //audio
                    codec = stream.Codec;
                    language = stream.Language;
                    channels = stream.Channels;
                    duration = stream.Duration;
                    samplingrate = stream.SamplingRate;
                    languagecode = stream.LanguageCode;
                    bitratemode = stream.BitrateMode;
                    selected = stream.Selected;
                    Default = stream.Default;
                    break;
                case "3":
                    //sub
                    title = stream.Title;
                    language = stream.Language;
                    codecid = stream.CodecID;
                    index = stream.Index;
                    codec = stream.Codec;
                    languagecode = stream.LanguageCode;
                    format = stream.Format;
                    selected = stream.Selected;
                    Default = stream.Default;
                    break;
            }
            id = stream.Id;
            type = stream.StreamType;
        }

        public static explicit operator Stream(PlexAndKodi.Stream stream_in)
        {
            Stream stream_out = new Stream();
            switch (stream_in.StreamType)
            {
                case "1":
                    stream_out.codec = stream_in.Codec;
                    stream_out.width = stream_in.Width;
                    stream_out.height = stream_in.Height;
                    stream_out.duration = stream_in.Duration;
                    stream_out.bitrate = stream_in.Bitrate;
                    stream_out.scantype = stream_in.ScanType;
                    stream_out.refframes = stream_in.RefFrames;
                    stream_out.profile = stream_in.Profile;
                    stream_out.level = stream_in.Level;
                    stream_out.frameratemode = stream_in.FrameRateMode;
                    stream_out.framerate = stream_in.FrameRate;
                    stream_out.colorspace = stream_in.ColorSpace;
                    stream_out.codecid = stream_in.CodecID;
                    stream_out.chromasubsampling = stream_in.ChromaSubsampling;
                    stream_out.cabac = stream_in.Cabac;
                    stream_out.bitdepth = stream_in.BitDepth;
                    stream_out.index = stream_in.Index;
                    stream_out.languagecode = stream_in.LanguageCode;
                    break;
                case "2":
                    stream_out.codec = stream_in.Codec;
                    stream_out.language = stream_in.Language;
                    stream_out.channels = stream_in.Channels;
                    stream_out.duration = stream_in.Duration;
                    stream_out.samplingrate = stream_in.SamplingRate;
                    stream_out.languagecode = stream_in.LanguageCode;
                    stream_out.bitratemode = stream_in.BitrateMode;
                    stream_out.selected = stream_in.Selected;
                    stream_out.Default = stream_in.Default;
                    break;
                case "3":
                    stream_out.title = stream_in.Title;
                    stream_out.language = stream_in.Language;
                    stream_out.codecid = stream_in.CodecID;
                    stream_out.index = stream_in.Index;
                    stream_out.codec = stream_in.Codec;
                    stream_out.languagecode = stream_in.LanguageCode;
                    stream_out.format = stream_in.Format;
                    stream_out.selected = stream_in.Selected;
                    stream_out.Default = stream_in.Default;
                    break;
            }
            stream_out.id = stream_in.Id;
            stream_out.type = stream_in.StreamType;

            return stream_out;
        }
    }
}

