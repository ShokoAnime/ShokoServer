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
    public class Part
    {
        /// <summary>
        /// key/url to content
        /// </summary>
        public string key;

        /// <summary>
        /// content container
        /// </summary>
        public string container;

        /// <summary>
        /// Stream list
        /// </summary>
        public List<Stream> Streams { get; set; }

        /// <summary>
        /// Parametraless constructor for XML Serialization
        /// </summary>
        public Part()
        {

        }

        /// <summary>
        /// Contructor that create Medias out of video
        /// </summary>
        public Part(JMMContracts.PlexAndKodi.Part part)
        {
            key = part.Key;
            container = part.Container;

            Streams = new List<Stream>();
            foreach (JMMContracts.PlexAndKodi.Stream stream in part.Streams)
            {
                Streams.Add(new Stream(stream));
            }
        }

        public static explicit operator Part(PlexAndKodi.Part part_in)
        {
            Part part_out = new Part();

            part_out.container = part_in.Container;
            part_out.key = part_in.Key;

            if (part_in.Streams != null)
            {
                part_out.Streams = new List<Stream>();
                foreach (JMMContracts.PlexAndKodi.Stream stream in part_in.Streams)
                {
                    part_out.Streams.Add((Stream)stream);
                }
            }

            return part_out;
        }
    }
}
