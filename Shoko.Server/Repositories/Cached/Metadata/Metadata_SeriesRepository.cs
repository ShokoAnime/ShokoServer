using Shoko.Abstractions.Metadata;
using Shoko.Server.Databases;
using Shoko.Server.Models.Metadata;

namespace Shoko.Server.Repositories.Cached.Metadata;

/// <summary>
///   Every plugin source's series in the store.
/// </summary>
/// <param name="databaseFactory">The database factory.</param>
public class Metadata_SeriesRepository(DatabaseFactory databaseFactory) : MetadataSourceRowRepository<Metadata_Series>(databaseFactory)
{
    /// <inheritdoc />
    protected override MetadataSource SourceOf(Metadata_Series row)
        => row.Source;

    /// <inheritdoc />
    protected override string ProviderIDOf(Metadata_Series row)
        => row.ProviderID;
}
