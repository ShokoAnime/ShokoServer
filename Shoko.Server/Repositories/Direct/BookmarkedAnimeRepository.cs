using System.Collections.Generic;
using System.Linq;
using NHibernate;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Repositories.Direct;

public class BookmarkedAnimeRepository : BaseDirectRepository<BookmarkedAnime, int>
{
    public BookmarkedAnime GetByAnimeID(int animeID)
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenStatelessSession();
            return session.Query<BookmarkedAnime>()
                .Where(a => a.AnimeID == animeID)
                .SingleOrDefault();
        });
    }

    public override IReadOnlyList<BookmarkedAnime> GetAll()
    {
        return base.GetAll().OrderBy(a => a.Priority).ToList();
    }

    public override IReadOnlyList<BookmarkedAnime> GetAll(ISession session)
    {
        return base.GetAll(session).OrderBy(a => a.Priority).ToList();
    }

    public override IReadOnlyList<BookmarkedAnime> GetAll(ISessionWrapper session)
    {
        return base.GetAll(session).OrderBy(a => a.Priority).ToList();
    }
}
