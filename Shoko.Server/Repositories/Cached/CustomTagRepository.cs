using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Repositories.NHibernate;

#nullable enable
namespace Shoko.Server.Repositories.Cached;

public class CustomTagRepository : BaseCachedRepository<CustomTag, int>
{
    private PocoIndex<int, CustomTag, string?>? _names;

    public CustomTagRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
        DeleteWithOpenTransactionCallback = (ses, obj) =>
        {
            RepoFactory.CrossRef_CustomTag.DeleteWithOpenTransaction(ses,
                RepoFactory.CrossRef_CustomTag.GetByCustomTagID(obj.CustomTagID));
        };
    }

    protected override int SelectKey(CustomTag entity)
    {
        return entity.CustomTagID;
    }

    public override void PopulateIndexes()
    {
        _names = new PocoIndex<int, CustomTag, string?>(Cache, a => a.TagName);
    }

    public override void RegenerateDb()
    {
    }

    public List<CustomTag> GetByAnimeID(int animeID)
    {
        return RepoFactory.CrossRef_CustomTag.GetByAnimeID(animeID)
            .Select(a => GetByID(a.CustomTagID))
            .Where(a => a != null)
            .ToList();
    }

    public CustomTag? GetByTagName(string? tagName)
        => !string.IsNullOrEmpty(tagName?.Trim())
            ? ReadLock(() => _names!.GetOne(tagName))
            : null;

    public Dictionary<int, List<CustomTag>> GetByAnimeIDs(ISessionWrapper session, int[] animeIDs)
    {
        return animeIDs.ToDictionary(a => a,
            a => RepoFactory.CrossRef_CustomTag.GetByAnimeID(a)
                .Select(b => GetByID(b.CustomTagID))
                .Where(b => b != null)
                .ToList());
    }
}
