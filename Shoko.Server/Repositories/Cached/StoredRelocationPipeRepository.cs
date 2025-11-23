using System;
using System.Collections.Generic;
using NutzCode.InMemoryIndex;
using Shoko.Server.Databases;
using Shoko.Server.Models;

#nullable enable
namespace Shoko.Server.Repositories.Direct;

public class StoredRelocationPipeRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<StoredRelocationPipe, int>(databaseFactory)
{
    private PocoIndex<int, StoredRelocationPipe, Guid>? _pipeIDs;
    private PocoIndex<int, StoredRelocationPipe, Guid>? _providerIDs;
    private PocoIndex<int, StoredRelocationPipe, string>? _names;

    protected override int SelectKey(StoredRelocationPipe entity)
        => entity.StoredRelocationPipeID;

    public override void PopulateIndexes()
    {
        _pipeIDs = Cache.CreateIndex(a => a.ID);
        _providerIDs = Cache.CreateIndex(a => a.ProviderID);
        _names = Cache.CreateIndex(a => a.Name);
    }

    public StoredRelocationPipe? GetByName(string? scriptName)
        => !string.IsNullOrEmpty(scriptName)
            ? Lock(() => _names!.GetOne(scriptName))
            : null;

    public StoredRelocationPipe? GetByPipeID(Guid pipeID)
        => Lock(() => _pipeIDs!.GetOne(pipeID));

    public IReadOnlyList<StoredRelocationPipe> GetByProviderID(Guid providerID)
        => Lock(() => _providerIDs!.GetMultiple(providerID));
}
