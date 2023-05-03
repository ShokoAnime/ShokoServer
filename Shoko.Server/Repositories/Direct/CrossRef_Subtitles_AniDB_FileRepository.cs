using System;
using System.Collections.Generic;
using System.Linq;
using NHibernate;
using Shoko.Models.Server;
using Shoko.Server.Databases;

namespace Shoko.Server.Repositories.Direct;

public class CrossRef_Subtitles_AniDB_FileRepository : BaseDirectRepository<CrossRef_Subtitles_AniDB_File, int>
{
    public List<CrossRef_Subtitles_AniDB_File> GetByFileID(int id)
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            return session
                .Query<CrossRef_Subtitles_AniDB_File>()
                .Where(a => a.FileID == id)
                .ToList();
        });
    }

    public Dictionary<int, HashSet<string>> GetLanguagesByAnime(IEnumerable<int> animeIds)
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            return session.CreateSQLQuery(@"SELECT DISTINCT eps.AnimeID, sub.LanguageName FROM CrossRef_File_Episode eps
INNER JOIN AniDB_File f ON f.Hash = eps.Hash
INNER JOIN CrossRef_Subtitles_AniDB_File sub on sub.FileID = f.FileID
WHERE eps.AnimeID IN (:animeIds)")
                .AddScalar("AnimeID", NHibernateUtil.Int32)
                .AddScalar("LanguageName", NHibernateUtil.String)
                .SetParameterList("animeIds", animeIds)
                .List<object[]>().GroupBy(a => (int)a[0], a => (string)a[1])
                .ToDictionary(a => a.Key, a => a.ToHashSet(StringComparer.InvariantCultureIgnoreCase));
        });
    }
    
    public HashSet<string> GetLanguagesForAnime(int animeID)
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            return session.CreateSQLQuery(@"SELECT DISTINCT sub.LanguageName FROM CrossRef_File_Episode eps
INNER JOIN AniDB_File f ON f.Hash = eps.Hash
INNER JOIN CrossRef_Subtitles_AniDB_File sub on sub.FileID = f.FileID
WHERE eps.AnimeID = :animeId")
                .SetParameter("animeId", animeID)
                .List<string>().ToHashSet(StringComparer.InvariantCultureIgnoreCase);
        });
    }
}
