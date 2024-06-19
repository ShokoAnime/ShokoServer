using System;
using System.Collections.Generic;
using System.Linq;
using NHibernate;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Extensions;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Models;

namespace Shoko.Server.Repositories.Cached;

public class CrossRef_Subtitles_AniDB_FileRepository : BaseCachedRepository<CrossRef_Subtitles_AniDB_File, int>
{
    private PocoIndex<int, CrossRef_Subtitles_AniDB_File, int> FileIDs;

    public List<CrossRef_Subtitles_AniDB_File> GetByFileID(int id)
    {
        return FileIDs.GetMultiple(id);
    }

    public HashSet<string> GetLanguagesForGroup(SVR_AnimeGroup group)
    {
        return ReadLock(() =>
        {
            return group.AllSeries.Select(a => a.AniDB_Anime).WhereNotNull().SelectMany(a => RepoFactory.CrossRef_File_Episode.GetByAnimeID(a.AnimeID))
                .Select(a => RepoFactory.AniDB_File.GetByHash(a.Hash)).WhereNotNull().SelectMany(a => GetByFileID(a.FileID)).Select(a => a.LanguageName)
                .ToHashSet(StringComparer.InvariantCultureIgnoreCase);
        });
    }
    
    public HashSet<string> GetLanguagesForAnime(int animeID)
    {
        return ReadLock(() =>
        {
            return RepoFactory.CrossRef_File_Episode.GetByAnimeID(animeID)?.Select(a => RepoFactory.AniDB_File.GetByHash(a.Hash)).WhereNotNull()
                .SelectMany(a => GetByFileID(a.FileID)).Select(a => a.LanguageName).ToHashSet(StringComparer.InvariantCultureIgnoreCase) ?? [];
        });
    }

    public override void PopulateIndexes()
    {
        FileIDs = Cache.CreateIndex(a => a.FileID);
    }

    public override void RegenerateDb() { }

    protected override int SelectKey(CrossRef_Subtitles_AniDB_File entity) => entity.CrossRef_Subtitles_AniDB_FileID;

    public CrossRef_Subtitles_AniDB_FileRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
