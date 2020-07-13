using System.ComponentModel.DataAnnotations;

namespace Shoko.Server.API.v3.Models.Common
{
    public class AVDumpResult
    {
        [Required]
        public string FullOutput { get; set; }
        
        [Required]
        public string Ed2k { get; set; }
    }
}