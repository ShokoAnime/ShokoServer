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
            Parts = new List<Part>();
            foreach (JMMContracts.PlexAndKodi.Part part in media.Parts)
            {
                Parts.Add(new Part(part));
            }
        }

        public static explicit operator Media(JMMContracts.PlexAndKodi.Media media_in)
        {
            Media media_out = new Media();
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
