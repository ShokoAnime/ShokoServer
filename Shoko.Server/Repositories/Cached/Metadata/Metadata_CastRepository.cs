using System.Collections.Generic;
using Shoko.Server.Databases;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Utilities;

namespace Shoko.Server.Repositories.Cached.Metadata;

/// <summary>
///   Every source's cast credits in the store.
/// </summary>
/// <param name="databaseFactory">The database factory.</param>
public class Metadata_CastRepository(DatabaseFactory databaseFactory) : MetadataEntryRowRepository<Metadata_Cast>(databaseFactory)
{
    private PocoIndex<int, Metadata_Cast, int>? _creatorIDs;

    private PocoIndex<int, Metadata_Cast, int>? _characterIDs;

    /// <inheritdoc />
    public override void PopulateIndexes()
    {
        base.PopulateIndexes();
        _creatorIDs = Cache.CreateIndex(row => row.CreatorID ?? 0);
        _characterIDs = Cache.CreateIndex(row => row.CharacterID ?? 0);
    }

    /// <summary>
    ///   Every row naming one creator.
    /// </summary>
    /// <param name="creatorID">The store's ID for it.</param>
    /// <returns>The rows, in no particular order.</returns>
    public IReadOnlyList<Metadata_Cast> GetByCreatorID(int creatorID)
        => creatorID is 0 ? [] : _creatorIDs!.GetMultiple(creatorID);

    /// <summary>
    ///   Every row naming one character.
    /// </summary>
    /// <param name="characterID">The store's ID for it.</param>
    /// <returns>The rows, in no particular order.</returns>
    public IReadOnlyList<Metadata_Cast> GetByCharacterID(int characterID)
        => characterID is 0 ? [] : _characterIDs!.GetMultiple(characterID);
}
