using System;
using System.Collections.Generic;
using NutzCode.InMemoryIndex;
using Shoko.Server.Databases;
using Shoko.Server.Models.Shoko;

namespace Shoko.Server.Repositories.Cached;

public class StoredRelocationPresetRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<StoredRelocationPreset, int>(databaseFactory)
{
    private PocoIndex<int, StoredRelocationPreset, Guid>? _presetIDs;

    private PocoIndex<int, StoredRelocationPreset, Guid>? _providerIDs;

    private PocoIndex<int, StoredRelocationPreset, bool>? _isDefault;

    private PocoIndex<int, StoredRelocationPreset, string>? _names;

    protected override int SelectKey(StoredRelocationPreset entity)
        => entity.StoredRelocationPresetID;

    public override void PopulateIndexes()
    {
        _presetIDs = Cache.CreateIndex(a => a.ID);
        _providerIDs = Cache.CreateIndex(a => a.ProviderID);
        _isDefault = Cache.CreateIndex(a => a.IsDefault);
        _names = Cache.CreateIndex(a => a.Name);
    }

    public StoredRelocationPreset? GetByName(string? scriptName)
        => !string.IsNullOrEmpty(scriptName)
            ? _names!.GetOne(scriptName)
            : null;

    public StoredRelocationPreset? GetByPresetID(Guid presetID)
        => _presetIDs!.GetOne(presetID);

    public IReadOnlyList<StoredRelocationPreset> GetByProviderID(Guid providerID)
        => _providerIDs!.GetMultiple(providerID);

    public StoredRelocationPreset? GetDefault()
        => _isDefault!.GetOne(true);
}
