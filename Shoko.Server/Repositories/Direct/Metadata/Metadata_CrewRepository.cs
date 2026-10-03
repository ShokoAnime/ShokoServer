using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models.Metadata;

namespace Shoko.Server.Repositories.Direct.Metadata;

/// <summary>
///   Every source's crew credits in the store.
/// </summary>
/// <param name="databaseFactory">The database factory.</param>
public class Metadata_CrewRepository(DatabaseFactory databaseFactory) : MetadataCreditRepository<Metadata_Crew>(databaseFactory)
{
    #region Reading

    /// <summary>
    ///   Every row naming one creator.
    /// </summary>
    /// <param name="creatorID">The store's ID for it.</param>
    /// <returns>The rows, in no particular order.</returns>
    public virtual IReadOnlyList<Metadata_Crew> GetByCreatorID(int creatorID)
    {
        if (creatorID is 0)
            return [];

        using var session = _databaseFactory.SessionFactory.OpenStatelessSession();
        return session.Query<Metadata_Crew>().Where(row => row.CreatorID == creatorID).ToList();
    }

    /// <summary>
    ///   Which of some creators any row names.
    /// </summary>
    /// <param name="creatorIDs">The store's IDs for the creators.</param>
    /// <returns>The IDs of those credited, each once.</returns>
    public virtual IReadOnlySet<int> GetCreditedCreatorIDs(IEnumerable<int> creatorIDs)
        => QueryInChunks(
            [.. creatorIDs.Where(id => id is not 0).Distinct()],
            (session, chunk) => session.Query<Metadata_Crew>()
                .Where(row => chunk.Contains(row.CreatorID))
                .Select(row => row.CreatorID)
                .Distinct()
                .ToList()
        ).ToHashSet();

    #endregion
}
