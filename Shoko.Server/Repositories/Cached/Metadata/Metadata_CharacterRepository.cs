using Shoko.Abstractions.Metadata;
using Shoko.Server.Databases;
using Shoko.Server.Models.Metadata;

namespace Shoko.Server.Repositories.Cached.Metadata;

/// <summary>
///   Every source's characters in the store.
/// </summary>
/// <param name="databaseFactory">The database factory.</param>
public class Metadata_CharacterRepository(DatabaseFactory databaseFactory) : MetadataSourceRowRepository<Metadata_Character>(databaseFactory)
{
    /// <inheritdoc />
    protected override MetadataSource SourceOf(Metadata_Character row)
        => row.Source;

    /// <inheritdoc />
    protected override string ProviderIDOf(Metadata_Character row)
        => row.ProviderID;
}
