using System.Collections.Generic;
using Shoko.Models.Server;

namespace Shoko.Models.Client
{
    public class CL_AniDB_Episode : AniDB_Episode
    {
        // Map of language to title, using AniDB languages
        // TODO Make an ENUM for AniDB_Language
        public Dictionary<string, string> Titles { get; set; }
    }
}
