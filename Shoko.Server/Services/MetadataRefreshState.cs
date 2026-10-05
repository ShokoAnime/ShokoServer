using System;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Services;

namespace Shoko.Server.Services;

/// <summary>
///   When each provider's entries were last refreshed, which the refresh jobs
///   read to skip an entry that is still fresh and write after a refresh.
/// </summary>
public interface IMetadataRefreshState
{
    /// <summary>
    ///   When an entry was last refreshed without failing: the
    ///   <c>LastRefreshedAt</c> of the entry it names.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The time, in UTC, or <c>null</c> when it never was or is not stored.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is <c>null</c>.</exception>
    DateTime? GetLastRefreshedAt(MetadataGuid entry);

    /// <summary>
    ///   Record that an entry was refreshed without failing, on its row in the
    ///   store keeping it.
    /// </summary>
    /// <param name="entry">The series, movie or collection, or the creator, character, studio or network.</param>
    /// <param name="refreshedAt">When the refresh finished.</param>
    /// <returns><c>true</c> if a stored row took it; <c>false</c> for an entry no store keeps.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is <c>null</c>.</exception>
    bool RecordRefresh(MetadataGuid entry, DateTime refreshedAt);
}

/// <summary>
///   Reads the refresh time off each entry and writes it on the stores' rows.
/// </summary>
/// <remarks>
///   The time goes with the row, so a purge forgets it. A core source keeps
///   its own tables, whose entries answer from them, so nothing is written
///   for them.
/// </remarks>
/// <param name="metadataService">Finds the entries, through the plugins' resolvers first.</param>
/// <param name="seriesStore">Writes a plugin source's series under its own lock.</param>
/// <param name="movieStore">Writes a plugin source's movies under its own lock.</param>
/// <param name="collectionStore">Writes a plugin source's collections under its own lock.</param>
/// <param name="peopleStore">Writes the creators and characters under its own lock.</param>
/// <param name="studioStore">Writes the studios and networks under its own lock.</param>
public class MetadataRefreshState(
    IMetadataService metadataService,
    MetadataSeriesStore seriesStore,
    MetadataMovieStore movieStore,
    MetadataCollectionStore collectionStore,
    MetadataPeopleStore peopleStore,
    MetadataStudioStore studioStore
) : IMetadataRefreshState
{
    #region Refresh State

    /// <summary>
    ///   How long a refreshed entry stays fresh: a refresh that is not forced
    ///   skips an entry refreshed more recently than this.
    /// </summary>
    public static readonly TimeSpan FreshFor = TimeSpan.FromHours(1);

    /// <summary>
    ///   Whether an entry last refreshed at a time is still fresh, so a
    ///   refresh that is not forced skips it.
    /// </summary>
    /// <param name="lastRefreshedAt">When it was last refreshed, or <c>null</c> when it never was.</param>
    /// <returns><c>true</c> when it was refreshed within <see cref="FreshFor"/>.</returns>
    public static bool IsFresh(DateTime? lastRefreshedAt)
        => lastRefreshedAt is { } last && DateTime.UtcNow - last.ToUniversalTime() < FreshFor;

    /// <summary>
    ///   Reads when an entry was last refreshed off the entry itself.
    /// </summary>
    /// <param name="entry">The entry, or <c>null</c>.</param>
    /// <returns>The time, in UTC, or <c>null</c> when there is none.</returns>
    public static DateTime? LastRefreshedAt(IMetadata? entry)
        => entry switch
        {
            ISeries series => series.LastRefreshedAt,
            IMovie movie => movie.LastRefreshedAt,
            ICollection collection => collection.LastRefreshedAt,
            IEpisode episode => episode.LastRefreshedAt,
            ISeason season => season.LastRefreshedAt,
            ICreator creator => creator.LastRefreshedAt,
            ICharacter character => character.LastRefreshedAt,
            IStudio studio => studio.LastRefreshedAt,
            INetwork network => network.LastRefreshedAt,
            _ => null,
        };

    /// <inheritdoc />
    public DateTime? GetLastRefreshedAt(MetadataGuid entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return LastRefreshedAt(metadataService.GetEntry(entry));
    }

    /// <inheritdoc />
    public bool RecordRefresh(MetadataGuid entry, DateTime refreshedAt)
    {
        ArgumentNullException.ThrowIfNull(entry);

        // The stores' rows keep local time, like their other dates.
        var localTime = refreshedAt.Kind is DateTimeKind.Utc ? refreshedAt.ToLocalTime() : refreshedAt;
        return entry.EntityType switch
        {
            _ when entry.EntityType == MetadataEntityType.Series => seriesStore.SetLastRefreshedAt(entry, localTime),
            _ when entry.EntityType == MetadataEntityType.Movie => movieStore.SetLastRefreshedAt(entry, localTime),
            _ when entry.EntityType == MetadataEntityType.Collection => collectionStore.SetLastRefreshedAt(entry, localTime),
            _ when entry.EntityType == MetadataEntityType.Creator || entry.EntityType == MetadataEntityType.Character =>
                peopleStore.SetLastRefreshedAt(entry, localTime),
            _ when entry.EntityType == MetadataEntityType.Studio || entry.EntityType == MetadataEntityType.Network =>
                studioStore.SetLastRefreshedAt(entry, localTime),
            _ => false,
        };
    }

    #endregion
}
