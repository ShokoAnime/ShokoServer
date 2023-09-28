using System;
using System.Collections.Generic;
using System.Linq;
using NHibernate;
using NutzCode.InMemoryIndex;
using Shoko.Models.Server;
using Shoko.Server.Databases;

namespace Shoko.Server.Repositories.Direct;

public class CrossRef_Languages_AniDB_FileRepository : BaseCachedRepository<CrossRef_Languages_AniDB_File, int>
{
    private PocoIndex<int, CrossRef_Languages_AniDB_File, int> FileIDs;

    public List<CrossRef_Languages_AniDB_File> GetByFileID(int id)
    {
        return FileIDs.GetMultiple(id);
    }
    
    public Dictionary<int, HashSet<string>> GetLanguagesByAnime(IEnumerable<int> animeIds)
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            return session.CreateSQLQuery(@"SELECT DISTINCT eps.AnimeID, lang.LanguageName
FROM CrossRef_File_Episode eps
INNER JOIN AniDB_File f ON f.Hash = eps.Hash
INNER JOIN CrossRef_Languages_AniDB_File lang on lang.FileID = f.FileID
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
            return session.CreateSQLQuery(@"SELECT DISTINCT lang.LanguageName
FROM CrossRef_File_Episode eps
INNER JOIN AniDB_File f ON f.Hash = eps.Hash
INNER JOIN CrossRef_Languages_AniDB_File lang on lang.FileID = f.FileID
WHERE eps.AnimeID = :animeId")
                .SetParameter("animeId", animeID)
                .List<string>().ToHashSet(StringComparer.InvariantCultureIgnoreCase);
        });
    }

    public override void PopulateIndexes()
    {
        FileIDs = Cache.CreateIndex(a => a.FileID);
    }

    public override void RegenerateDb() { }

    protected override int SelectKey(CrossRef_Languages_AniDB_File entity) => entity.CrossRef_Languages_AniDB_FileID;
}
