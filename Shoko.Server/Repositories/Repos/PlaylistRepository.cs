using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Server;

namespace Shoko.Server.Repositories.Repos
{
    public class PlaylistRepository : BaseRepository<Playlist, int>
    {
        internal override int SelectKey(Playlist entity) => entity.PlaylistID;
            
        internal override void PopulateIndexes()
        {
        }

        internal override void ClearIndexes()
        {
        }


        public override List<Playlist> GetAll()
        {
            return base.GetAll().OrderBy(a => a.PlaylistName).ToList();
        }
    }
}