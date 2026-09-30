using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Server.Databases;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Utilities;

namespace Shoko.Server.Repositories.Cached.TMDB;

/// <summary>
/// TMDB's movie collections, kept in memory, since a series' text walk asks
/// which collection each of its movies is in.
/// </summary>
/// <param name="databaseFactory">The database factory.</param>
public class TMDB_CollectionRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<TMDB_Collection, int>(databaseFactory)
{
    private PocoIndex<int, TMDB_Collection, int>? _collectionIDs;

    protected override int SelectKey(TMDB_Collection entity)
        => entity.TMDB_CollectionID;

    public override void PopulateIndexes()
    {
        _collectionIDs = Cache.CreateIndex(collection => collection.TmdbCollectionID);
    }

    /// <inheritdoc />
    /// <remarks>
    ///   A series linked to a movie may be named by the movie's collection,
    ///   read only once the collection is stored, so the movies in it are
    ///   forgotten with it.
    /// </remarks>
    protected override IEnumerable<MetadataGuid> TextEntriesOf(TMDB_Collection entity, bool removed)
        => [((IMetadata)entity).ID, .. RepoFactory.TMDB_Movie.GetByTmdbCollectionID(entity.TmdbCollectionID).Select(movie => ((IMetadata)movie).ID)];

    /// <summary>
    /// The collection TMDB knows by an ID.
    /// </summary>
    /// <param name="collectionId">The TMDB collection ID.</param>
    /// <returns>The collection, or <see langword="null"/> when it is not stored.</returns>
    public TMDB_Collection? GetByTmdbCollectionID(int collectionId)
        => _collectionIDs!.GetOne(collectionId);
}
