using System.ComponentModel.DataAnnotations;

namespace Shoko.Server.API.v3
{
    /// <summary>
    /// Rating object. Shared between sources, episodes vs series, etc
    /// </summary>
    public class Rating
    {
        /// <summary>
        /// rating
        /// </summary>
        [Required]
        public decimal Value { get; set; }
        
        /// <summary>
        /// out of what? Assuming int, as the max should be
        /// </summary>
        [Required]
        public int MaxValue { get; set; }
        
        /// <summary>
        /// AniDB, etc
        /// </summary>
        [Required]
        public string Source { get; set; }
        
        /// <summary>
        /// number of votes
        /// </summary>
        public int Votes { get; set; }
        
        /// <summary>
        /// for temporary vs permanent, or any other situations that may arise later
        /// </summary>
        public string Type { get; set; }
    }
}