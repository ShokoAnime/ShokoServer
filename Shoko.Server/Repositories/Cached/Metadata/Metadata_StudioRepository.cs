using Shoko.Abstractions.Metadata;
using Shoko.Server.Databases;
using Shoko.Server.Models.Metadata;

namespace Shoko.Server.Repositories.Cached.Metadata;

/// <summary>
///   Every source's studios in the store.
/// </summary>
/// <param name="databaseFactory">The database factory.</param>
public class Metadata_StudioRepository(DatabaseFactory databaseFactory) : MetadataSourceRowRepository<Metadata_Studio>(databaseFactory)
{
    /// <inheritdoc />
    protected override MetadataSource SourceOf(Metadata_Studio row)
        => row.Source;

    /// <inheritdoc />
    protected override string ProviderIDOf(Metadata_Studio row)
        => row.ProviderID;
}
