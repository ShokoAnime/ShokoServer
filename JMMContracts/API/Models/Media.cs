using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMMContracts.API.Models
{
    /// <summary>
    /// Model that represends Media
    /// </summary>
    public class Media
    {
        /// <summary>
        /// Parts list
        /// </summary>
        public List<Part> Parts { get; set; }
        public string duration { get; private set; }
        public string videoframerate { get; private set; }
        public string container { get; private set; }
        public string audiocodec { get; private set; }
        public string videocodec { get; private set; }
        public string audiochannels { get; private set; }
        public string aspectratio { get; private set; }
        public string height { get; private set; }
        public string bitrate { get; private set; }
        public string id { get; private set; }
        public string width { get; private set; }
        public string videoresolution { get; private set; }

        /// <summary>
        /// Parametraless constructor for XML Serialization
        /// </summary>
        public Media()
        {

        }

        /// <summary>
        /// Contructor that create Medias out of video
        /// </summary>
        public Media(JMMContracts.PlexAndKodi.Media media)
        {
            duration = media.Duration;
            videoframerate = media.VideoFrameRate;
            container = media.Container;
            videocodec = media.VideoCodec;
            audiocodec = media.AudioCodec;
            audiochannels = media.AudioChannels;
            aspectratio = media.AspectRatio;
            height = media.Height;
            width = media.Width;
            bitrate = media.Bitrate;
            id = media.Id;
            videoresolution = media.VideoResolution;

            Parts = new List<Part>();
            foreach (JMMContracts.PlexAndKodi.Part part in media.Parts)
            {
                Parts.Add(new Part(part));
            }
        }

        public static explicit operator Media(JMMContracts.PlexAndKodi.Media media_in)
        {
            Media media_out = new Media();

            media_out.duration = media_in.Duration;
            media_out.videoframerate = media_in.VideoFrameRate;
            media_out.container = media_in.Container;
            media_out.videocodec = media_in.VideoCodec;
            media_out.audiocodec = media_in.AudioCodec;
            media_out.audiochannels = media_in.AudioChannels;
            media_out.aspectratio = media_in.AspectRatio;
            media_out.height = media_in.Height;
            media_out.width = media_in.Width;
            media_out.bitrate = media_in.Bitrate;
            media_out.id = media_in.Id;
            media_out.videoresolution = media_in.VideoResolution;

            if (media_in.Parts != null)
            {
                media_out.Parts = new List<Part>();
                foreach (JMMContracts.PlexAndKodi.Part part in media_in.Parts)
                {
                    media_out.Parts.Add((Part)part);
                }
            }
            return media_out;
        }
    }
}
