using Shoko.Abstractions.Metadata;
using Shoko.Server.Databases;
using Shoko.Server.Models.Metadata;

namespace Shoko.Server.Repositories.Cached.Metadata;

/// <summary>
///   Every source's tags and genres in the store.
/// </summary>
/// <param name="databaseFactory">The database factory.</param>
public class Metadata_TagRepository(DatabaseFactory databaseFactory) : MetadataSourceRowRepository<Metadata_Tag>(databaseFactory)
{
    /// <inheritdoc />
    protected override MetadataSource SourceOf(Metadata_Tag row)
        => row.Source;

    /// <inheritdoc />
    protected override string ProviderIDOf(Metadata_Tag row)
        => row.ProviderID;
}
