using Shoko.Server.Databases;
using Shoko.Server.Models.Metadata;

namespace Shoko.Server.Repositories.Cached.Metadata;

/// <summary>
///   Every plugin source's content ratings on its stored series and movies.
/// </summary>
/// <param name="databaseFactory">The database factory.</param>
public class Metadata_ContentRatingRepository(DatabaseFactory databaseFactory) : MetadataEntryRowRepository<Metadata_ContentRating>(databaseFactory);
