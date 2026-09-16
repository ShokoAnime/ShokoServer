using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Databases;
using Shoko.Server.Models.Airing;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Repositories.Cached.Airing;

/// <summary>
/// Cached repository for <see cref="AiringSchedule"/>. Every read it offers is
/// a lookup on one of the indexes below, so nothing scans the cache.
/// </summary>
/// <param name="databaseFactory">The database factory.</param>
public class AiringScheduleRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<AiringSchedule, int>(databaseFactory)
{
    private PocoIndex<int, AiringSchedule, Guid>? _scheduleIDs;

    private PocoIndex<int, AiringSchedule, (DataSource SeriesSource, string SeriesID)>? _seriesKeys;

    private PocoIndex<int, AiringSchedule, (DataSource SeriesSource, string SeriesID, string SeasonID)>? _seriesSeasonKeys;

    private PocoIndex<int, AiringSchedule, Guid>? _providerIDs;

    private PocoIndex<int, AiringSchedule, Guid?>? _channelIDs;

    /// <inheritdoc/>
    protected override int SelectKey(AiringSchedule entity)
        => entity.AiringScheduleID;

    /// <inheritdoc/>
    public override void PopulateIndexes()
    {
        _scheduleIDs = Cache.CreateIndex(a => a.ID);
        _seriesKeys = Cache.CreateIndex(a => (a.SeriesSource, a.SeriesID));
        _seriesSeasonKeys = Cache.CreateIndex(a => (a.SeriesSource, a.SeriesID, a.SeasonID));
        _providerIDs = Cache.CreateIndex(a => a.ProviderID);
        _channelIDs = Cache.CreateIndex(a => a.ChannelID);
    }

    /// <summary>
    /// Gets the schedule with the given public ID, which also answers "does
    /// this provider already have a schedule for this series, season and key",
    /// since the ID derives from exactly that.
    /// </summary>
    /// <param name="scheduleID">The public ID of the schedule.</param>
    /// <returns>The schedule, or <c>null</c> when there is none.</returns>
    public AiringSchedule? GetByScheduleID(Guid scheduleID)
        => _scheduleIDs!.GetOne(scheduleID);

    /// <summary>
    /// Gets every schedule for a series, whatever season they are narrowed to.
    /// </summary>
    /// <param name="seriesSource">The source of the series.</param>
    /// <param name="seriesID">The ID of the series within its source.</param>
    /// <returns>The schedules, ordered by provider and key.</returns>
    public IReadOnlyList<AiringSchedule> GetBySeriesID(DataSource seriesSource, string seriesID)
        => string.IsNullOrEmpty(seriesID)
            ? []
            : _seriesKeys!.GetMultiple((seriesSource, seriesID))
                .OrderBy(a => a.ProviderName, StringComparer.Ordinal)
                .ThenBy(a => a.Key, StringComparer.Ordinal)
                .ToList();

    /// <summary>
    /// Gets every schedule for a series narrowed to the given season. Pass
    /// <c>null</c> or an empty season ID for the schedules covering the whole
    /// run.
    /// </summary>
    /// <param name="seriesSource">The source of the series.</param>
    /// <param name="seriesID">The ID of the series within its source.</param>
    /// <param name="seasonID">The ID of the season within its source, or <c>null</c> for the whole run.</param>
    /// <returns>The schedules, ordered by provider and key.</returns>
    public IReadOnlyList<AiringSchedule> GetBySeriesIDAndSeasonID(DataSource seriesSource, string seriesID, string? seasonID)
        => string.IsNullOrEmpty(seriesID)
            ? []
            : _seriesSeasonKeys!.GetMultiple((seriesSource, seriesID, seasonID ?? string.Empty))
                .OrderBy(a => a.ProviderName, StringComparer.Ordinal)
                .ThenBy(a => a.Key, StringComparer.Ordinal)
                .ToList();

    /// <summary>
    /// Gets every schedule owned by a provider, which is what removing a
    /// provider's data walks.
    /// </summary>
    /// <param name="providerID">The ID of the provider.</param>
    /// <returns>The schedules, ordered by series and key.</returns>
    public IReadOnlyList<AiringSchedule> GetByProviderID(Guid providerID)
        => _providerIDs!.GetMultiple(providerID)
            .OrderBy(a => a.SeriesSource)
            .ThenBy(a => a.SeriesID, StringComparer.Ordinal)
            .ThenBy(a => a.Key, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Gets every schedule airing on a channel.
    /// </summary>
    /// <param name="channelID">The ID of the channel.</param>
    /// <returns>The schedules, ordered by series and key.</returns>
    public IReadOnlyList<AiringSchedule> GetByChannelID(Guid channelID)
        => _channelIDs!.GetMultiple(channelID)
            .OrderBy(a => a.SeriesSource)
            .ThenBy(a => a.SeriesID, StringComparer.Ordinal)
            .ThenBy(a => a.Key, StringComparer.Ordinal)
            .ToList();
}
