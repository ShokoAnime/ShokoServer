using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models.Metadata;

namespace Shoko.Server.Repositories.Direct.Metadata;

/// <summary>
///   Every source's cast credits in the store.
/// </summary>
/// <param name="databaseFactory">The database factory.</param>
public class Metadata_CastRepository(DatabaseFactory databaseFactory) : MetadataCreditRepository<Metadata_Cast>(databaseFactory)
{
    #region Reading

    /// <summary>
    ///   Every row naming one creator.
    /// </summary>
    /// <param name="creatorID">The store's ID for it.</param>
    /// <returns>The rows, in no particular order.</returns>
    public virtual IReadOnlyList<Metadata_Cast> GetByCreatorID(int creatorID)
    {
        if (creatorID is 0)
            return [];

        using var session = _databaseFactory.SessionFactory.OpenStatelessSession();
        return session.Query<Metadata_Cast>().Where(row => row.CreatorID == creatorID).ToList();
    }

    /// <summary>
    ///   Every row naming one character.
    /// </summary>
    /// <param name="characterID">The store's ID for it.</param>
    /// <returns>The rows, in no particular order.</returns>
    public virtual IReadOnlyList<Metadata_Cast> GetByCharacterID(int characterID)
    {
        if (characterID is 0)
            return [];

        using var session = _databaseFactory.SessionFactory.OpenStatelessSession();
        return session.Query<Metadata_Cast>().Where(row => row.CharacterID == characterID).ToList();
    }

    /// <summary>
    ///   Which of some creators any row names.
    /// </summary>
    /// <param name="creatorIDs">The store's IDs for the creators.</param>
    /// <returns>The IDs of those credited, each once.</returns>
    public virtual IReadOnlySet<int> GetCreditedCreatorIDs(IEnumerable<int> creatorIDs)
        => QueryInChunks(
            [.. creatorIDs.Where(id => id is not 0).Distinct()],
            (session, chunk) => session.Query<Metadata_Cast>()
                .Where(row => row.CreatorID != null && chunk.Contains(row.CreatorID.Value))
                .Select(row => row.CreatorID!.Value)
                .Distinct()
                .ToList()
        ).ToHashSet();

    /// <summary>
    ///   Which of some characters any row names.
    /// </summary>
    /// <param name="characterIDs">The store's IDs for the characters.</param>
    /// <returns>The IDs of those credited, each once.</returns>
    public virtual IReadOnlySet<int> GetCreditedCharacterIDs(IEnumerable<int> characterIDs)
        => QueryInChunks(
            [.. characterIDs.Where(id => id is not 0).Distinct()],
            (session, chunk) => session.Query<Metadata_Cast>()
                .Where(row => row.CharacterID != null && chunk.Contains(row.CharacterID.Value))
                .Select(row => row.CharacterID!.Value)
                .Distinct()
                .ToList()
        ).ToHashSet();

    #endregion
}
