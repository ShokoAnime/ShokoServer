using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JMMModels.Childs;

namespace JMMModels
{
    public class AniDB_Character : CharacterInfo
    {
        public List<AnimeWithCreatorInfo> Animes { get; set; }
    }
}
