using System.ComponentModel.DataAnnotations;

namespace Shoko.Server.API.v3
{
    /// <summary>
    /// A collection of the IDs that are relevant to an object. Each one Extends this if needed.
    /// All models should use this, even if there's only one ID. It's just more consistent that way.
    /// </summary>
    public class IDs
    {
        /// <summary>
        /// The Shoko internal ID, for easy lookup
        /// </summary>
        [Required]
        public int ID { get; set; }
    }
}