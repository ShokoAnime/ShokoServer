using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shoko.Models.Server;

namespace Shoko.Models.Client
{
    public class CL_AniDB_Anime_Relation : AniDB_Anime_Relation
    {
        public CL_AniDB_Anime AniDB_Anime { get; set; }
        public CL_AnimeSeries_User AnimeSeries { get; set; }

    }
}
