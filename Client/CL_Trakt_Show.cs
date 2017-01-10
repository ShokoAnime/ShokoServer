using System.Collections.Generic;
using Shoko.Models.Server;

namespace Shoko.Models.Client
{
    public class CL_Trakt_Show : Trakt_Show
    {
        public List<CL_Trakt_Season> Seasons { get; set; }
    }
}