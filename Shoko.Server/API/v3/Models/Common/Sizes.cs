using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

namespace Shoko.Server.API.v3
{
    /// <summary>
    /// Downloaded, Watched, Total, etc
    /// </summary>
    public class Sizes
    {
        /// <summary>
        /// What is downloaded and available
        /// </summary>
        [Required]
        public EpisodeCounts local { get; set; }

        /// <summary>
        /// What is watched (must also be local
        /// </summary>
        [Required]
        public EpisodeCounts watched { get; set; }

        /// <summary>
        /// how many total
        /// </summary>
        [Required]
        public EpisodeCounts total { get; set; }

        /// <summary>
        /// Lists the count of each type of episode. None are required. If not present, the total count is 0
        /// </summary>
        public class EpisodeCounts
        {
            public int episodes { get; set; }

            public int specials { get; set; }

            public int credits { get; set; }

            public int trailers { get; set; }

            public int parodies { get; set; }

            public int others { get; set; }
        }
    }
}