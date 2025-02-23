using System.Collections.Generic;
using Shoko.Models.Server;

namespace Shoko.Models.Client
{
    public class CL_Trakt_Season : Trakt_Season
    {
        public List<Trakt_Episode> Episodes { get; set; }
    }
}