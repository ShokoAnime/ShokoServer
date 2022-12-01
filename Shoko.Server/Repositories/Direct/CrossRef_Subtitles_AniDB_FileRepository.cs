using System.Collections.Generic;
using System.Linq;
using NHibernate;
using NHibernate.Criterion;
using Shoko.Models.Server;
using Shoko.Server.Databases;

namespace Shoko.Server.Repositories.Direct;

public class CrossRef_Subtitles_AniDB_FileRepository : BaseDirectRepository<CrossRef_Subtitles_AniDB_File, int>
{
    public List<CrossRef_Subtitles_AniDB_File> GetByFileID(int id)
    {
        lock (GlobalDBLock)
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            var files = session
                .CreateCriteria(typeof(CrossRef_Subtitles_AniDB_File))
                .Add(Restrictions.Eq("FileID", id))
                .List<CrossRef_Subtitles_AniDB_File>();

            return new List<CrossRef_Subtitles_AniDB_File>(files);
        }
    }

    public Dictionary<int, HashSet<string>> GetLanguagesByAnime(IEnumerable<int> animeIds)
    {
        lock (GlobalDBLock)
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            return session.CreateSQLQuery(string.Format(
                    @"SELECT DISTINCT xref.AnimeID, l.LanguageName
FROM CrossRef_File_Episode xref
    INNER JOIN AniDB_File f ON f.Hash = xref.Hash
    INNER JOIN CrossRef_Subtitles_AniDB_File l ON l.FileID = f.FileID
WHERE xref.AnimeID IN ({0})", string.Join(",", animeIds)))
                .AddScalar("AnimeID", NHibernateUtil.Int32)
                .AddScalar("LanguageName", NHibernateUtil.String)
                .List<object[]>()
                .GroupBy(a => (int)a[0], a => (string)a[1])
                .ToDictionary(a => a.Key, grouping => grouping.ToHashSet());
        }
    }
}
