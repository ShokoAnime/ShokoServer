using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Shoko.Server.API.v3.Models.Common
{
    public class AVDumpResult
    {
        [Required]
        public string FullOutput { get; set; }
        
        public IEnumerable<string> E2DkResults { get; set; }
    }
}