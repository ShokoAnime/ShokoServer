using System.ComponentModel.DataAnnotations;

namespace Shoko.Server.API.v3
{
    /// <summary>
    /// The most basic info about a series.
    /// Used to determine if we want to get the data for it.
    /// Anything more complex that filter by type and restricted will require group filters or pulling the data first 
    /// </summary>
    public class SeriesStub : BaseStub
    {
        public override string type => "Series";
        
        /// <summary>
        /// Series type. Series, OVA, Movie, etc
        /// </summary>
        [Required]
        public string series_type { get; set; }

        /// <summary>
        /// Is it porn...or close enough
        /// If not provided, assume no
        /// </summary>
        public bool restricted { get; set; }
    }
}