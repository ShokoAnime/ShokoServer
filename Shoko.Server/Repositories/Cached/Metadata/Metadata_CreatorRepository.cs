using Shoko.Abstractions.Metadata;
using Shoko.Server.Databases;
using Shoko.Server.Models.Metadata;

namespace Shoko.Server.Repositories.Cached.Metadata;

/// <summary>
///   Every source's creators in the store.
/// </summary>
/// <param name="databaseFactory">The database factory.</param>
public class Metadata_CreatorRepository(DatabaseFactory databaseFactory) : MetadataSourceRowRepository<Metadata_Creator>(databaseFactory)
{
    /// <inheritdoc />
    protected override MetadataSource SourceOf(Metadata_Creator row)
        => row.Source;

    /// <inheritdoc />
    protected override string ProviderIDOf(Metadata_Creator row)
        => row.ProviderID;
}
