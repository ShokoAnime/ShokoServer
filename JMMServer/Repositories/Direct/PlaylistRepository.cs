using System.Collections.Generic;
using System.Linq;
using JMMServer.Entities;
using JMMServer.Repositories.NHibernate;
using NHibernate;

namespace JMMServer.Repositories.Direct
{
    public class PlaylistRepository : BaseDirectRepository<Playlist, int>
    {
        public override List<Playlist> GetAll()
        {
            return base.GetAll().OrderBy(a => a.PlaylistName).ToList();
        }
        public override List<Playlist> GetAll(ISession session)
        {
            return base.GetAll(session).OrderBy(a => a.PlaylistName).ToList();
        }
        public override List<Playlist> GetAll(ISessionWrapper session)
        {
            return base.GetAll(session).OrderBy(a => a.PlaylistName).ToList();
        }
    }
}