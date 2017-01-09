using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shoko.Models.Server;

namespace Shoko.Models.Client
{
    public class CL_AniDB_Character : AniDB_Character
    {
        // from AniDB_Anime_Character
        public string CharType { get; set; }

        public AniDB_Seiyuu Seiyuu { get; set; }
        public CL_AniDB_Anime Anime { get; set; }
    }
}
