using Shoko.Abstractions.Metadata;
using Shoko.Server.Databases;
using Shoko.Server.Models.Metadata;

namespace Shoko.Server.Repositories.Cached.Metadata;

/// <summary>
///   Every plugin source's movies in the store.
/// </summary>
/// <param name="databaseFactory">The database factory.</param>
public class Metadata_MovieRepository(DatabaseFactory databaseFactory) : MetadataSourceRowRepository<Metadata_Movie>(databaseFactory)
{
    /// <inheritdoc />
    protected override MetadataSource SourceOf(Metadata_Movie row)
        => row.Source;

    /// <inheritdoc />
    protected override string ProviderIDOf(Metadata_Movie row)
        => row.ProviderID;
}
