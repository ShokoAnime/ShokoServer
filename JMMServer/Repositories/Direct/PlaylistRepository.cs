using System.Collections.Generic;
using System.Linq;
using JMMServer.Entities;
using Shoko.Models.Server;
using JMMServer.Repositories.NHibernate;
using NHibernate;

namespace JMMServer.Repositories.Direct
{
    public class PlaylistRepository : BaseDirectRepository<Playlist, int>
    {
        private PlaylistRepository()
        {
            
        }

        public static PlaylistRepository Create()
        {
            return new PlaylistRepository();
        }
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