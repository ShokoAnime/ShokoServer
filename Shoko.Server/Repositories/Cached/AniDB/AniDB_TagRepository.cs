#nullable enable
using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Extensions;

namespace Shoko.Server.Repositories.Cached.AniDB;

public class AniDB_TagRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<AniDB_Tag, int>(databaseFactory)
{
    private PocoIndex<int, AniDB_Tag, int>? _tagIDs;

    private PocoIndex<int, AniDB_Tag, string>? _names;

    private PocoIndex<int, AniDB_Tag, string>? _sourceNames;

    protected override int SelectKey(AniDB_Tag entity)
        => entity.AniDB_TagID;

    public override void PopulateIndexes()
    {
        _tagIDs = Cache.CreateIndex(a => a.TagID);
        _names = Cache.CreateIndex(a => a.TagName);
        _sourceNames = Cache.CreateIndex(a => a.TagNameSource);
    }

    public override void RegenerateDb()
    {
        var tags = Cache.Values
            .Where(tag => (tag.TagDescription?.Contains('`') ?? false) || tag.TagName.Contains('`'))
            .ToList();
        foreach (var tag in tags)
        {
            tag.TagDescription = tag.TagDescription?.Replace('`', '\'');
            tag.TagNameOverride = tag.TagNameOverride?.Replace('`', '\'');
            tag.TagNameSource = tag.TagNameSource.Replace('`', '\'');
            Save(tag);
        }
    }

    public AniDB_Tag? GetByTagID(int tagID)
        => ReadLock(() => _tagIDs!.GetOne(tagID));

    public IReadOnlyList<AniDB_Tag> GetByName(string name)
        => ReadLock(() => _names!.GetMultiple(name));

    public IReadOnlyList<AniDB_Tag> GetBySourceName(string sourceName)
        => ReadLock(() => _sourceNames!.GetMultiple(sourceName));

    /// <summary>
    /// Gets all the tags, but only if we have the anime locally
    /// </summary>
    /// <returns></returns>
    public IReadOnlyList<AniDB_Tag> GetAllForLocalSeries()
        => RepoFactory.AnimeSeries.GetAll()
            .SelectMany(a => RepoFactory.AniDB_Anime_Tag.GetByAnimeID(a.AniDB_ID))
            .WhereNotNull()
            .Select(a => GetByTagID(a.TagID))
            .WhereNotNull()
            .DistinctBy(a => a.TagID)
            .ToList();
}
