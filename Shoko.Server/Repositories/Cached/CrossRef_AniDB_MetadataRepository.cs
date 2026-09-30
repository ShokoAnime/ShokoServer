using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Server.Databases;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Services;
using Shoko.Server.Utilities;

namespace Shoko.Server.Repositories.Cached;

/// <summary>
/// What every level's cross-reference repository can do, whichever source the
/// links belong to.
/// </summary>
/// <typeparam name="TRow">The level's row.</typeparam>
/// <param name="databaseFactory">The database factory.</param>
public abstract class BaseCrossRef_AniDB_MetadataRepository<TRow>(DatabaseFactory databaseFactory) : BaseCachedRepository<TRow, int>(databaseFactory)
    where TRow : CrossRef_AniDB_Metadata, new()
{
    private PocoIndex<int, TRow, int>? _anidbAnimeIDs;

    private PocoIndex<int, TRow, (MetadataSource, string)>? _providerIDs;

    public override void PopulateIndexes()
    {
        _anidbAnimeIDs = Cache.CreateIndex(xref => xref.AnidbAnimeID);
        _providerIDs = Cache.CreateIndex(xref => (xref.Source, xref.ProviderID));
    }

    /// <summary>
    /// Every link an anime has at this level.
    /// </summary>
    /// <param name="anidbAnimeID">The AniDB anime ID.</param>
    /// <param name="source">One source, or every source when left out.</param>
    /// <returns>The links, in the order they were stored.</returns>
    public IReadOnlyList<TRow> GetByAnidbAnimeID(int anidbAnimeID, MetadataSource? source = null)
        => Filter(_anidbAnimeIDs!.GetMultiple(anidbAnimeID), source);

    /// <summary>
    /// Every link at this level pointing at one of a provider's entries.
    /// </summary>
    /// <param name="source">The source the entry belongs to.</param>
    /// <param name="providerID">The provider's own ID for it.</param>
    /// <returns>The links, in the order they were stored.</returns>
    public IReadOnlyList<TRow> GetByProviderID(MetadataSource source, string providerID)
        => string.IsNullOrEmpty(providerID) ? [] : _providerIDs!.GetMultiple((source, providerID));

    /// <summary>
    /// Every link sitting in one entry's slot, in the order they sit in.
    /// </summary>
    /// <param name="slot">The entry the links are counted within.</param>
    /// <returns>The links, best first.</returns>
    internal abstract IReadOnlyList<TRow> GetBySlot((MetadataSource Source, int AnidbAnimeID, int AnidbEpisodeID) slot);

    /// <inheritdoc />
    /// <remarks>
    ///   What a Shoko entry is called follows its links, so the anime and
    ///   episode a link hangs off are forgotten, and with them what reads
    ///   them.
    /// </remarks>
    protected override IEnumerable<MetadataGuid> TextEntriesOf(TRow entity, bool removed)
    {
        yield return MetadataTextManager.AnidbAnimeID(entity.AnidbAnimeID);
        switch (entity)
        {
            case CrossRef_AniDB_Metadata_Episode episode:
                yield return MetadataTextManager.AnidbEpisodeID(episode.AnidbEpisodeID);
                break;
            case CrossRef_AniDB_Metadata_Movie movie:
                yield return MetadataTextManager.AnidbEpisodeID(movie.AnidbEpisodeID);
                break;
        }
    }

    private protected static IReadOnlyList<TRow> Filter(IReadOnlyList<TRow> xrefs, MetadataSource? source)
        => source is not { } onlySource ? xrefs : [.. xrefs.Where(xref => xref.Source == onlySource)];
}

/// <summary>
/// Every series-level metadata cross-reference, whichever source it belongs to.
/// </summary>
/// <param name="databaseFactory">The database factory.</param>
public class CrossRef_AniDB_Metadata_SeriesRepository(DatabaseFactory databaseFactory) : BaseCrossRef_AniDB_MetadataRepository<CrossRef_AniDB_Metadata_Series>(databaseFactory)
{
    protected override int SelectKey(CrossRef_AniDB_Metadata_Series entity)
        => entity.CrossRef_AniDB_Metadata_SeriesID;

    /// <inheritdoc />
    internal override IReadOnlyList<CrossRef_AniDB_Metadata_Series> GetBySlot((MetadataSource Source, int AnidbAnimeID, int AnidbEpisodeID) slot)
        => [.. GetByAnidbAnimeID(slot.AnidbAnimeID, slot.Source).OrderBy(xref => xref.Ordering)];
}

/// <summary>
/// Every film-level metadata cross-reference, whichever source it belongs to.
/// </summary>
/// <param name="databaseFactory">The database factory.</param>
public class CrossRef_AniDB_Metadata_MovieRepository(DatabaseFactory databaseFactory) : BaseCrossRef_AniDB_MetadataRepository<CrossRef_AniDB_Metadata_Movie>(databaseFactory)
{
    private PocoIndex<int, CrossRef_AniDB_Metadata_Movie, int>? _anidbEpisodeIDs;

    protected override int SelectKey(CrossRef_AniDB_Metadata_Movie entity)
        => entity.CrossRef_AniDB_Metadata_MovieID;

    public override void PopulateIndexes()
    {
        base.PopulateIndexes();
        _anidbEpisodeIDs = Cache.CreateIndex(xref => xref.AnidbEpisodeID);
    }

    /// <summary>
    /// Every film an episode stands for.
    /// </summary>
    /// <param name="anidbEpisodeID">The AniDB episode ID.</param>
    /// <param name="source">One source, or every source when left out.</param>
    /// <returns>The links, in the order they were stored.</returns>
    public IReadOnlyList<CrossRef_AniDB_Metadata_Movie> GetByAnidbEpisodeID(int anidbEpisodeID, MetadataSource? source = null)
        => anidbEpisodeID is 0 ? [] : Filter(_anidbEpisodeIDs!.GetMultiple(anidbEpisodeID), source);

    /// <inheritdoc />
    internal override IReadOnlyList<CrossRef_AniDB_Metadata_Movie> GetBySlot((MetadataSource Source, int AnidbAnimeID, int AnidbEpisodeID) slot)
        => [.. GetByAnidbEpisodeID(slot.AnidbEpisodeID, slot.Source).OrderBy(xref => xref.Ordering)];
}

/// <summary>
/// Every episode-level metadata cross-reference, whichever source it belongs
/// to.
/// </summary>
/// <param name="databaseFactory">The database factory.</param>
public class CrossRef_AniDB_Metadata_EpisodeRepository(DatabaseFactory databaseFactory) : BaseCrossRef_AniDB_MetadataRepository<CrossRef_AniDB_Metadata_Episode>(databaseFactory)
{
    private PocoIndex<int, CrossRef_AniDB_Metadata_Episode, int>? _anidbEpisodeIDs;

    private PocoIndex<int, CrossRef_AniDB_Metadata_Episode, (MetadataSource, string)>? _providerParentIDs;

    protected override int SelectKey(CrossRef_AniDB_Metadata_Episode entity)
        => entity.CrossRef_AniDB_Metadata_EpisodeID;

    public override void PopulateIndexes()
    {
        base.PopulateIndexes();
        _anidbEpisodeIDs = Cache.CreateIndex(xref => xref.AnidbEpisodeID);
        _providerParentIDs = Cache.CreateIndex(xref => (xref.Source, xref.ProviderParentID));
    }

    /// <summary>
    /// Every link pointing into one of a provider's works, such as a show.
    /// </summary>
    /// <param name="source">The source the work belongs to.</param>
    /// <param name="providerParentID">The provider's own ID for it.</param>
    /// <returns>The links, in the order they were stored.</returns>
    public IReadOnlyList<CrossRef_AniDB_Metadata_Episode> GetByProviderParentID(MetadataSource source, string providerParentID)
        => string.IsNullOrEmpty(providerParentID) ? [] : _providerParentIDs!.GetMultiple((source, providerParentID));

    /// <summary>
    /// Every link an episode has.
    /// </summary>
    /// <param name="anidbEpisodeID">The AniDB episode ID.</param>
    /// <param name="source">One source, or every source when left out.</param>
    /// <returns>The links, in the order they were stored.</returns>
    public IReadOnlyList<CrossRef_AniDB_Metadata_Episode> GetByAnidbEpisodeID(int anidbEpisodeID, MetadataSource? source = null)
        => anidbEpisodeID is 0 ? [] : Filter(_anidbEpisodeIDs!.GetMultiple(anidbEpisodeID), source);

    /// <inheritdoc />
    internal override IReadOnlyList<CrossRef_AniDB_Metadata_Episode> GetBySlot((MetadataSource Source, int AnidbAnimeID, int AnidbEpisodeID) slot)
        => [.. GetByAnidbEpisodeID(slot.AnidbEpisodeID, slot.Source).OrderBy(xref => xref.Ordering)];
}
