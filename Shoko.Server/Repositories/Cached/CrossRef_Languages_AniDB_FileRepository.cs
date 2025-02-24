using System;
using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Extensions;
using Shoko.Server.Models;

#nullable enable
namespace Shoko.Server.Repositories.Cached;

public class CrossRef_Languages_AniDB_FileRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<CrossRef_Languages_AniDB_File, int>(databaseFactory)
{
    private PocoIndex<int, CrossRef_Languages_AniDB_File, int>? _fileIDs;

    protected override int SelectKey(CrossRef_Languages_AniDB_File entity)
        => entity.CrossRef_Languages_AniDB_FileID;

    public override void PopulateIndexes()
    {
        _fileIDs = Cache.CreateIndex(a => a.FileID);
    }

    public List<CrossRef_Languages_AniDB_File> GetByFileID(int id)
        => ReadLock(() => _fileIDs!.GetMultiple(id));

    public HashSet<string> GetLanguagesForGroup(SVR_AnimeGroup group)
        => ReadLock(() => group.AllSeries
            .Select(a => a.AniDB_Anime)
            .WhereNotNull()
            .SelectMany(a => RepoFactory.CrossRef_File_Episode.GetByAnimeID(a.AnimeID))
            .Select(a => RepoFactory.AniDB_File.GetByHash(a.Hash))
            .WhereNotNull()
            .SelectMany(a => GetByFileID(a.FileID))
            .Select(a => a.LanguageName)
            .ToHashSet(StringComparer.InvariantCultureIgnoreCase)
        );

    public HashSet<string> GetLanguagesForAnime(int animeID)
        => ReadLock(() => RepoFactory.CrossRef_File_Episode.GetByAnimeID(animeID)
            .Select(a => RepoFactory.AniDB_File.GetByHash(a.Hash))
            .WhereNotNull()
            .SelectMany(a => GetByFileID(a.FileID))
            .Select(a => a.LanguageName)
            .ToHashSet(StringComparer.InvariantCultureIgnoreCase)
        );
}
