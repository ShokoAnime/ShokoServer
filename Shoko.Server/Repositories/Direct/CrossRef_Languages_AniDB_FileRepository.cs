using System;
using System.Collections.Generic;
using System.Linq;
using NHibernate.Criterion;
using Shoko.Models.Server;
using Shoko.Server.Databases;

namespace Shoko.Server.Repositories.Direct;

public class CrossRef_Languages_AniDB_FileRepository : BaseDirectRepository<CrossRef_Languages_AniDB_File, int>
{
    public List<CrossRef_Languages_AniDB_File> GetByFileID(int id)
    {
        lock (GlobalDBLock)
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            var files = session
                .CreateCriteria(typeof(CrossRef_Languages_AniDB_File))
                .Add(Restrictions.Eq("FileID", id))
                .List<CrossRef_Languages_AniDB_File>();

            return new List<CrossRef_Languages_AniDB_File>(files);
        }
    }
    
    public Dictionary<int, HashSet<string>> GetLanguagesByAnime(IEnumerable<int> animeIds)
    {
        return animeIds
            .SelectMany(a =>
                RepoFactory.CrossRef_File_Episode.GetByAnimeID(a)
                    .SelectMany(b =>
                        RepoFactory.AniDB_File.GetByHash(b.Hash)?.Languages?.Select(c => c.LanguageName) ??
                        Array.Empty<string>()).Select(c => (AnimeID: a, Language: c)))
            .GroupBy(a => a.AnimeID, a => a.Language).ToDictionary(a => a.Key, a => a.ToHashSet());
    }
}
