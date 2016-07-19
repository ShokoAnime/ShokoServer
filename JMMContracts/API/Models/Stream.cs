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
        /// <summary>
        /// id
        /// </summary>
        public string id { get; set; }

        /// <summary>
        /// type of stream 1 video, 2 audio, 3 subs
        /// </summary>
        public string type { get; set; }

        /// <summary>
        /// videoCodec
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string videoCodec { get; set; }

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
        /// Audio Codec
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string AudioCodec { get; set; }

        /// <summary>
        /// Language of audio
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string AudioLanguage { get; set; }

        /// <summary>
        /// Numers of audio channels
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string AudioChannels { get; set; }

        /// <summary>
        /// Language of subtitles 
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Language { get; set; }

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
                    videoCodec = stream.Codec;
                    width = stream.Width;
                    height = stream.Height;
                    duration = stream.Duration;
                    break;
                case "2":
                    //audio
                    AudioCodec = stream.Codec;
                    AudioLanguage = stream.Language;
                    AudioChannels = stream.Channels;
                    break;
                case "3":
                    //sub
                    Language = stream.Language;
                    break;
            }
            id = stream.Id;
            type = stream.StreamType;
        }

        public static explicit operator Stream(PlexAndKodi.Stream stream_in)
        {
            Stream stream_out = new Stream();

            stream_out.id = stream_in.Id;
            stream_out.AudioChannels = stream_in.Channels;
            stream_out.AudioCodec = stream_in.Codec;
            stream_out.AudioLanguage = stream_in.Language;
            stream_out.duration = stream_in.Duration;
            stream_out.height = stream_in.Height;
            stream_out.Language = stream_in.Language;
            stream_out.type = stream_in.StreamType;
            stream_out.videoCodec = stream_in.Codec;
            stream_out.width = stream_in.Width;

            return stream_out;
        }
    }
}

