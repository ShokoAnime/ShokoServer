using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMMContracts.API.Models
{
    /// <summary>
    /// Model that represents a Filters
    /// </summary>
    public class Filters
    {
        /// <summary>
        /// Filter count
        /// </summary>
        public int count { get; set; }
        
        /// <summary>
        /// Hold Filter list
        /// </summary>
        public List<JMMContracts.API.Models.Filter> groups { get; set; }

        /// <summary>
        /// Parametraless constructor for XML Serialization
        /// </summary>
        public Filters()
        {

        }

        /// <summary>
        /// Constructor that create Filters out of MediaContainer
        /// </summary>
        public Filters(JMMContracts.PlexAndKodi.MediaContainer mc)
        {
            if (mc.Childrens != null)
            {
                count = mc.Childrens.Count;

                groups = new List<Filter>();
                foreach (PlexAndKodi.Video video in mc.Childrens)
                {
                    groups.Add(new Filter(video));
                }
            }
            else
            {
                count = 0;
            }
        }
      
    }
}
