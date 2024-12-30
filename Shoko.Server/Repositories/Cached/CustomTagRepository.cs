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

    protected override int SelectKey(CustomTag entity)
        => entity.CustomTagID;

    public CustomTagRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
        DeleteWithOpenTransactionCallback = (ses, obj) =>
            RepoFactory.CrossRef_CustomTag.DeleteWithOpenTransaction(ses, RepoFactory.CrossRef_CustomTag.GetByCustomTagID(obj.CustomTagID));
    }

    public override void PopulateIndexes()
    {
        _names = new PocoIndex<int, CustomTag, string?>(Cache, a => a.TagName);
    }

    public List<CustomTag> GetByAnimeID(int animeID)
        => RepoFactory.CrossRef_CustomTag.GetByAnimeID(animeID)
            .Select(a => GetByID(a.CustomTagID))
            .Where(a => a != null)
            .ToList();

    public CustomTag? GetByTagName(string? tagName)
        => !string.IsNullOrWhiteSpace(tagName)
            ? ReadLock(() => _names!.GetOne(tagName))
            : null;
}
