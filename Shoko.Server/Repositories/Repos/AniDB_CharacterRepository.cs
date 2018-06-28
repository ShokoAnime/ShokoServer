using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Client;
using Shoko.Models.Server;
using Shoko.Server.Extensions;

namespace Shoko.Server.Repositories.Repos
{
    public class AniDB_CharacterRepository : BaseRepository<AniDB_Character, int>
    {
        internal override int SelectKey(AniDB_Character entity) => entity.CharID;
        
        internal override void PopulateIndexes()
        {
        }

        internal override void ClearIndexes()
        {
        }

        public ILookup<int, AnimeCharacterAndSeiyuu> GetCharacterAndSeiyuuByAnime(IEnumerable<int> animeIds)
        {

            if (animeIds == null)
                throw new ArgumentNullException(nameof(animeIds));

            //TODO Optimize this shit
            Dictionary<int, List<(int, string)>> animechars = Repo.AniDB_Anime_Character.GetCharsByAnimesIDs(animeIds);
            List<int> charids = animechars.Values.SelectMany(a => a.Select(b=>b.Item1)).Distinct().ToList();
            Dictionary<int, AniDB_Character> chars = GetMany(charids).ToDictionary(a => a.CharID, a => a);
            Dictionary<int, List<int>> charseiyuus = Repo.AniDB_Character_Seiyuu.GetSeiyuusFromCharIds(chars.Keys);
            List<int> seyuuids = charseiyuus.Values.SelectMany(a => a).Distinct().ToList();
            Dictionary<int, AniDB_Seiyuu> seiyuus=Repo.AniDB_Seiyuu.GetMany(seyuuids).ToDictionary(a=> a.SeiyuuID,a=>a);
            List<AnimeCharacterAndSeiyuu> ls=new List<AnimeCharacterAndSeiyuu>();
            foreach (int animeid in animechars.Keys)
            {
                if (animechars.ContainsKey(animeid))
                {
                    foreach ((int, string) tp in animechars[animeid])
                    {
                        if (chars.ContainsKey(tp.Item1) && charseiyuus.ContainsKey(tp.Item1))
                        {
                            AniDB_Character chr = chars[tp.Item1];
                            List<int> seius = charseiyuus[tp.Item1];
                            foreach (AniDB_Seiyuu s in seiyuus.Where(a => seius.Contains(a.Key)).Select(a => a.Value))
                            {
                                ls.Add(new AnimeCharacterAndSeiyuu(animeid,chr,s,tp.Item2));
                            }
                        }
                    }
                }
            }

            return ls.ToLookup(a => a.AnimeID, a => a);
        }

        internal AniDB_Character GetByCharID(int charID)
        {
            return Where(a => a.CharID == charID).FirstOrDefault();
        }
    }

    public class AnimeCharacterAndSeiyuu
    {
        public AnimeCharacterAndSeiyuu(int animeID, AniDB_Character character, AniDB_Seiyuu seiyuu = null,
            string characterType = null)
        {
            if (character == null)
                throw new ArgumentNullException(nameof(character));

            AnimeID = animeID;
            Character = character;
            Seiyuu = seiyuu;
            CharacterType = characterType ?? String.Empty;
        }

        public CL_AniDB_Character ToClient()
        {
            return Character.ToClient(CharacterType, Seiyuu);
        }

        public int AnimeID { get;  }

        public AniDB_Character Character { get;  }

        public AniDB_Seiyuu Seiyuu { get; }

        public string CharacterType { get; }
    }
}