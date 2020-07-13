using System.ComponentModel.DataAnnotations;

namespace Shoko.Server.API.v3.Models.Common
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
        public EpisodeCounts Local { get; set; }

        /// <summary>
        /// What is watched (must also be local)
        /// </summary>
        public EpisodeCounts Watched { get; set; }

        /// <summary>
        /// how many total
        /// </summary>
        [Required]
        public EpisodeCounts Total { get; set; }

        /// <summary>
        /// Lists the count of each type of episode. None are required. If not present, the total count is 0
        /// </summary>
        public class EpisodeCounts
        {
            public int Episodes { get; set; }

            public int Specials { get; set; }

            public int Credits { get; set; }

            public int Trailers { get; set; }

            public int Parodies { get; set; }

            public int Others { get; set; }
        }
    }
}