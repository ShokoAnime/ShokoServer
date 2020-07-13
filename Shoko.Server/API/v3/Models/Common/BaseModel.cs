using System.ComponentModel.DataAnnotations;

namespace Shoko.Server.API.v3.Models.Common
{
    public abstract class BaseModel
    {
        /// <summary>
        /// The server's title. This will use overrides, the naming settings, MainTitle if all else fails. This is a guaranteed fallback
        /// </summary>
        [Required]
        public string Name { get; set; }
        
        /// <summary>
        /// number of direct children (number of series in group, eps in series, etc)
        /// </summary>
        [Required]
        public int Size { get; set; }
        
        /// <summary>
        /// Sizes object, has totals
        /// </summary>
        public Sizes Sizes { get; set; }
    }
}