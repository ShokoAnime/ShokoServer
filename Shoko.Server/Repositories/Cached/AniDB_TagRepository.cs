using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Collections;
using Shoko.Models.Server;
using Shoko.Server.API;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Utilities;

namespace Shoko.Server.Repositories.Cached;

public class AniDB_TagRepository : BaseCachedRepository<AniDB_Tag, int>
{
    private PocoIndex<int, AniDB_Tag, int> Tags;
    private PocoIndex<int, AniDB_Tag, string> Names;
    private PocoIndex<int, AniDB_Tag, string> SourceNames;

    public override void PopulateIndexes()
    {
        Tags = new PocoIndex<int, AniDB_Tag, int>(Cache, a => a.TagID);
        Names = new PocoIndex<int, AniDB_Tag, string>(Cache, a => a.TagName);
        SourceNames = new PocoIndex<int, AniDB_Tag, string>(Cache, a => a.TagNameSource);
    }

    protected override int SelectKey(AniDB_Tag entity)
    {
        return entity.AniDB_TagID;
    }

    public override void RegenerateDb()
    {
        var tags = Cache.Values.Where(tag => (tag.TagDescription?.Contains('`') ?? false) || tag.TagName.Contains('`'))
            .ToList();
        foreach (var tag in tags)
        {
            tag.TagDescription = tag.TagDescription?.Replace('`', '\'');
            tag.TagNameOverride = tag.TagNameOverride?.Replace('`', '\'');
            tag.TagNameSource = tag.TagNameSource.Replace('`', '\'');
            Save(tag);
        }
    }

    public List<AniDB_Tag> GetByAnimeID(int animeID)
    {
        return RepoFactory.AniDB_Anime_Tag.GetByAnimeID(animeID)
            .Select(a => GetByTagID(a.TagID))
            .Where(a => a != null)
            .ToList();
    }


    public ILookup<int, AniDB_Tag> GetByAnimeIDs(int[] ids)
    {
        if (ids == null)
        {
            throw new ArgumentNullException(nameof(ids));
        }

        if (ids.Length == 0)
        {
            return EmptyLookup<int, AniDB_Tag>.Instance;
        }

        return RepoFactory.AniDB_Anime_Tag.GetByAnimeIDs(ids).SelectMany(a => a.ToList())
            .ToLookup(t => t.AnimeID, t => GetByTagID(t.TagID));
    }


    public AniDB_Tag GetByTagID(int id)
    {
        return ReadLock(() => Tags.GetOne(id));
    }

    public List<AniDB_Tag> GetByName(string name)
    {
        return ReadLock(() => Names.GetMultiple(name));
    }

    public List<AniDB_Tag> GetBySourceName(string sourceName)
    {
        return ReadLock(() => SourceNames.GetMultiple(sourceName));
    }

    /// <summary>
    /// Gets all the tags, but only if we have the anime locally
    /// </summary>
    /// <returns></returns>
    public List<AniDB_Tag> GetAllForLocalSeries(bool useUser = false)
    {
        IEnumerable<SVR_AnimeSeries> series;
        if (useUser)
        {
            var user = Utils.ServiceContainer.GetService<IHttpContextAccessor>()?.HttpContext?.GetUser();
            series = RepoFactory.AnimeSeries.GetAll().Where(a => user?.AllowedSeries(a) ?? true);
        }
        else
        {
            series = RepoFactory.AnimeSeries.GetAll();
        }

        return series
            .SelectMany(a => RepoFactory.AniDB_Anime_Tag.GetByAnimeID(a.AniDB_ID))
            .Where(a => a != null)
            .Select(a => GetByTagID(a.TagID))
            .Where(a => a != null)
            .DistinctBy(a => a.TagID)
            .ToList();
    }

    public AniDB_TagRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
