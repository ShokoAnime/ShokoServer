using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMMModels.Childs
{
    public class CharacterInfo : ImageInfo
    {
        public string Id { get; set; }
        public string RomajiName { get; set; }
        public string KanjiName { get; set; }
        public string PicName { get; set; }
        public string Description { get; set; }
        public AniDB_Character_Type? Type { get; set; }
        public AniDB_Character_Gender? Gender { get; set; }
        public HashSet<string> EpisodeIds { get; set; }
        public DateTime LastUpdate { get; set; }
    }


    public class AnimeWithCreatorInfo : CreatorInfo
    {
        public string AniDBId { get; set; }

    }
    public class CreatorInfo : AniDB_Creator
    {        
        public AniDB_Apparence_Type ApparenceType { get; set; }
        public bool IsMainSeiyuu { get; set; }
    }

}
    