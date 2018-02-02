using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Server;

namespace Shoko.Server.Repositories.Repos
{
    public class CustomTagRepository : BaseRepository<CustomTag, int>
    {
        internal override int SelectKey(CustomTag entity) => entity.CustomTagID;

        internal override void EndDelete(CustomTag entity, object returnFromBeginDelete, object parameters)
        {
            Repo.CrossRef_CustomTag.FindAndDelete(()=>Repo.CrossRef_CustomTag.GetByCustomTagID(entity.CustomTagID));
        }

        internal override void PopulateIndexes()
        {
        }

        internal override void ClearIndexes()
        {
        }

        public List<CustomTag> GetByAnimeID(int animeID)
        {
            return Repo.CrossRef_CustomTag.GetByAnimeID(animeID).Select(a => GetByID(a.CustomTagID)).Where(a => a != null).ToList();
        }


        public Dictionary<int, List<CustomTag>> GetByAnimeIDs(int[] animeIDs)
        {
            return animeIDs.ToDictionary(a => a,
                a => Repo.CrossRef_CustomTag.GetByAnimeID(a).Select(b => GetByID(b.CustomTagID)).Where(b => b != null).ToList());
        }
    }
}