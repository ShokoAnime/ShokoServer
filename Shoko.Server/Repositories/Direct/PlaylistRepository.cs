using System.Collections.Generic;
using System.Linq;
using NHibernate;
using Shoko.Models.Server;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Repositories.Direct
{
    public class PlaylistRepository : BaseDirectRepository<Playlist, int>
    {
        public override IReadOnlyList<Playlist> GetAll()
        {
            return base.GetAll().OrderBy(a => a.PlaylistName).ToList();
        }

        public override IReadOnlyList<Playlist> GetAll(ISession session)
        {
            return base.GetAll(session).OrderBy(a => a.PlaylistName).ToList();
        }

        public override IReadOnlyList<Playlist> GetAll(ISessionWrapper session)
        {
            return base.GetAll(session).OrderBy(a => a.PlaylistName).ToList();
        }
    }
}