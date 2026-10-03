using System;
using System.Collections.Generic;
using Moq;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.Metadata;
using Shoko.Server.Services;

namespace Shoko.Tests.Infrastructure;

/// <summary>
/// The real cross-reference store over in-memory tables that take writes, for
/// tests of what the store stores and what it refuses.
/// </summary>
public sealed class WritableLinkStore
{
    #region Constructors

    /// <summary>
    /// Builds the store, reporting its writes to a tracker when one is given.
    /// </summary>
    /// <param name="linkChanges">Where the store reports the links it changed.</param>
    public WritableLinkStore(MetadataLinkChangeTracker? linkChanges = null)
    {
        Series = Writable<CrossRef_AniDB_Metadata_SeriesRepository, CrossRef_AniDB_Metadata_Series>(
            row => row.CrossRef_AniDB_Metadata_SeriesID,
            (row, id) => row.CrossRef_AniDB_Metadata_SeriesID = id
        );
        Movies = Writable<CrossRef_AniDB_Metadata_MovieRepository, CrossRef_AniDB_Metadata_Movie>(
            row => row.CrossRef_AniDB_Metadata_MovieID,
            (row, id) => row.CrossRef_AniDB_Metadata_MovieID = id
        );
        Episodes = Writable<CrossRef_AniDB_Metadata_EpisodeRepository, CrossRef_AniDB_Metadata_Episode>(
            row => row.CrossRef_AniDB_Metadata_EpisodeID,
            (row, id) => row.CrossRef_AniDB_Metadata_EpisodeID = id
        );
        Store = new(Series, Movies, Episodes, CachedRepo.Build<Metadata_EpisodeRepository, int, Metadata_Episode>(row => row.Metadata_EpisodeID), linkChanges);
    }

    #endregion

    #region Properties

    /// <summary>
    /// The store.
    /// </summary>
    public MetadataCrossReferenceStore Store { get; }

    /// <summary>
    /// The series-level rows.
    /// </summary>
    public CrossRef_AniDB_Metadata_SeriesRepository Series { get; }

    /// <summary>
    /// The film-level rows.
    /// </summary>
    public CrossRef_AniDB_Metadata_MovieRepository Movies { get; }

    /// <summary>
    /// The episode-level rows.
    /// </summary>
    public CrossRef_AniDB_Metadata_EpisodeRepository Episodes { get; }

    #endregion

    #region Helpers

    /// <summary>
    /// A cache-backed repository whose saves give new rows their IDs the way
    /// the database would.
    /// </summary>
    /// <typeparam name="TRepo">The repository.</typeparam>
    /// <typeparam name="TRow">The row it holds.</typeparam>
    /// <param name="keyOf">A row's ID.</param>
    /// <param name="assignID">Gives a row its ID.</param>
    /// <returns>The repository.</returns>
    private static TRepo Writable<TRepo, TRow>(Func<TRow, int> keyOf, Action<TRow, int> assignID)
        where TRepo : BaseCachedRepository<TRow, int>
        where TRow : class, new()
    {
        var mock = CachedRepo.BuildWritable<TRepo, int, TRow>(keyOf);
        var repository = mock.Object;
        var nextID = 1;
        void Save(TRow row)
        {
            if (keyOf(row) is 0)
                assignID(row, nextID++);
            repository.Cache.Update(row);
        }

        mock.Setup(r => r.Save(It.IsAny<TRow>())).Callback<TRow>(Save);
        mock.Setup(r => r.Save(It.IsAny<IReadOnlyCollection<TRow>>())).Callback<IReadOnlyCollection<TRow>>(rows =>
        {
            foreach (var row in rows)
                Save(row);
        });
        mock.Setup(r => r.Delete(It.IsAny<IReadOnlyCollection<TRow>>())).Callback<IReadOnlyCollection<TRow>>(rows =>
        {
            foreach (var row in rows)
                repository.Cache.Remove(row);
        });
        return repository;
    }

    #endregion
}
