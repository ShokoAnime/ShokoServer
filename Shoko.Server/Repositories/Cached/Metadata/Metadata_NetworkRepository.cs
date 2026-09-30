using Shoko.Abstractions.Metadata;
using Shoko.Server.Databases;
using Shoko.Server.Models.Metadata;

namespace Shoko.Server.Repositories.Cached.Metadata;

/// <summary>
///   Every source's networks in the store.
/// </summary>
/// <param name="databaseFactory">The database factory.</param>
public class Metadata_NetworkRepository(DatabaseFactory databaseFactory) : MetadataSourceRowRepository<Metadata_Network>(databaseFactory)
{
    /// <inheritdoc />
    protected override MetadataSource SourceOf(Metadata_Network row)
        => row.Source;

    /// <inheritdoc />
    protected override string ProviderIDOf(Metadata_Network row)
        => row.ProviderID;
}
