using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Utilities;

namespace Shoko.Abstractions.Metadata.Services;

/// <summary>
///   Responsible for managing airing schedules across all providers, and the
///   single point of entry for everything schedule related: the shared channel
///   registry, the schedules themselves, their episode airings, the links
///   between airings, and refreshing.
/// </summary>
/// <remarks>
///   Providers fetch in their own jobs and push what they find through this
///   service, so the normal flow is
///   <see cref="FindOrRegisterChannel"/> →
///   <see cref="AddOrUpdateSchedule"/> →
///   <see cref="SetAirings"/>, with
///   <see cref="LinkAirings"/> where one slot covers several episodes. Every
///   change takes the provider instance and is checked against the registered
///   object and the owner stored on the schedule.
/// </remarks>
public interface IAiringScheduleService
{
    #region Parts

    /// <summary>
    ///   Adds the airing schedule providers and entity resolvers.
    /// </summary>
    /// <remarks>
    ///   This should be called once per instance of the service, and will be
    ///   called during start-up. Calling it multiple times will have no effect.
    /// </remarks>
    /// <param name="providers">
    ///   The airing schedule providers.
    /// </param>
    /// <param name="resolvers">
    ///   The airing schedule entity resolvers.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="providers"/> or <paramref name="resolvers"/> is
    ///   <c>null</c>.
    /// </exception>
    void AddParts(IEnumerable<IAiringScheduleProvider> providers, IEnumerable<IAiringScheduleEntityResolver> resolvers);

    #endregion

    #region Providers

    /// <summary>
    ///   Event raised when the available providers, their priority or their
    ///   enabled kinds are updated.
    /// </summary>
    event EventHandler? ProvidersUpdated;

    /// <summary>
    ///   Event raised once per finished chunk of a core-driven sweep, for a
    ///   provider implementing
    ///   <see cref="ISweepingAiringScheduleProvider"/>. Nothing about a sweep is
    ///   stored beyond what it takes to resume one, so this is the only record
    ///   there is.
    /// </summary>
    event EventHandler<AiringScheduleSweepEventArgs>? SweepCompleted;

    /// <summary>
    ///   List out all available providers, which kinds they have enabled, and
    ///   their source order.
    /// </summary>
    /// <param name="onlyEnabled">
    ///   If true, only providers with at least one enabled kind will be
    ///   returned.
    /// </param>
    /// <returns>
    ///   An enumerable of <see cref="AiringScheduleProviderInfo"/>s, one for
    ///   each available <see cref="IAiringScheduleProvider"/>.
    /// </returns>
    IEnumerable<AiringScheduleProviderInfo> GetAvailableProviders(bool onlyEnabled = false);

    /// <summary>
    ///   Gets the <see cref="AiringScheduleProviderInfo"/>s for every provider
    ///   belonging to the plugin.
    /// </summary>
    /// <param name="plugin">
    ///   The plugin.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="plugin"/> is <c>null</c>.
    /// </exception>
    /// <returns>
    ///   The provider infos, which is empty if the plugin has none.
    /// </returns>
    IReadOnlyList<AiringScheduleProviderInfo> GetProviderInfo(IPlugin plugin);

    /// <summary>
    ///   Gets the <see cref="AiringScheduleProviderInfo"/> for the specified
    ///   ID.
    /// </summary>
    /// <param name="providerID">
    ///   The ID of the provider.
    /// </param>
    /// <returns>
    ///   The provider info, or <c>null</c> if none could be found.
    /// </returns>
    AiringScheduleProviderInfo? GetProviderInfo(Guid providerID);

    /// <summary>
    ///   Gets the <see cref="AiringScheduleProviderInfo"/> for the provider.
    /// </summary>
    /// <param name="provider">
    ///   The provider.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="provider"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   Parts have not been added yet.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <paramref name="provider"/> is not the registered instance.
    /// </exception>
    /// <returns>
    ///   The provider info.
    /// </returns>
    AiringScheduleProviderInfo GetProviderInfo(IAiringScheduleProvider provider);

    /// <summary>
    ///   Gets the <see cref="AiringScheduleProviderInfo"/> for the specified
    ///   type.
    /// </summary>
    /// <typeparam name="TProvider">
    ///   The provider type.
    /// </typeparam>
    /// <exception cref="InvalidOperationException">
    ///   Parts have not been added yet.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <typeparamref name="TProvider"/> is unregistered.
    /// </exception>
    /// <returns>
    ///   The provider info.
    /// </returns>
    AiringScheduleProviderInfo GetProviderInfo<TProvider>() where TProvider : class, IAiringScheduleProvider;

    /// <summary>
    ///   Edit the settings for one or more
    ///   <see cref="IAiringScheduleProvider"/>s, such as which kinds are
    ///   enabled and the source order.
    /// </summary>
    /// <param name="providers">
    ///   The provider infos to persist.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="providers"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   One of the infos does not carry a registered provider instance.
    /// </exception>
    void UpdateProviders(params AiringScheduleProviderInfo[] providers);

    #endregion

    #region Channels

    /// <summary>
    ///   Event raised when a channel is registered, or when its aliases are
    ///   updated.
    /// </summary>
    event EventHandler<AiringChannelEventArgs>? ChannelRegistered;

    /// <summary>
    ///   Gets the channel with the given name and type, registering it if it is
    ///   unknown. The lookup is alias-blind: if another channel of the same
    ///   type holds the name as an alias, that alias is dropped and the new
    ///   channel keeps the name.
    /// </summary>
    /// <param name="name">
    ///   The display name of the channel, which is kept as given when the
    ///   channel is new. Use <see cref="GetRegionalChannelName"/> for a
    ///   regional service.
    /// </param>
    /// <param name="type">
    ///   The type of the channel, which is part of its identity.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="name"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   Parts have not been added yet.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <paramref name="name"/> is blank once normalised.
    /// </exception>
    /// <returns>
    ///   The existing channel, or the newly registered one.
    /// </returns>
    IAiringChannel FindOrRegisterChannel(string name, AiringChannelType type);

    /// <summary>
    ///   Gets the channel with the given ID.
    /// </summary>
    /// <param name="channelID">
    ///   The ID of the channel.
    /// </param>
    /// <returns>
    ///   The channel, or <c>null</c> if none could be found.
    /// </returns>
    IAiringChannel? GetChannelByID(Guid channelID);

    /// <summary>
    ///   Gets the channel with the given name or alias, of the given type. Own
    ///   names are matched before aliases.
    /// </summary>
    /// <param name="nameOrAlias">
    ///   The name or alias of the channel.
    /// </param>
    /// <param name="type">
    ///   The type of the channel.
    /// </param>
    /// <param name="useAliases">
    ///   If <c>false</c>, only own names are matched.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="nameOrAlias"/> is <c>null</c>.
    /// </exception>
    /// <returns>
    ///   The channel, or <c>null</c> if none could be found.
    /// </returns>
    IAiringChannel? GetChannelByName(string nameOrAlias, AiringChannelType type, bool useAliases = true);

    /// <summary>
    ///   Gets every registered channel, optionally of one type only.
    /// </summary>
    /// <param name="type">
    ///   Optional. If set, only channels of this type are returned.
    /// </param>
    /// <returns>
    ///   The registered channels.
    /// </returns>
    IReadOnlyList<IAiringChannel> GetAllChannels(AiringChannelType? type = null);

    /// <summary>
    ///   Adds aliases to a channel. An alias equal to the channel's own name,
    ///   or one it already has, is ignored.
    /// </summary>
    /// <param name="channel">
    ///   The channel to add the aliases to.
    /// </param>
    /// <param name="aliases">
    ///   The aliases to add.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="channel"/> or <paramref name="aliases"/> is
    ///   <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <paramref name="channel"/> is not registered.
    /// </exception>
    /// <exception cref="ChannelAliasConflictException">
    ///   An alias is another channel's own name, or another channel's alias, of
    ///   the same type.
    /// </exception>
    /// <returns>
    ///   The updated channel.
    /// </returns>
    IAiringChannel AddChannelAliases(IAiringChannel channel, IEnumerable<string> aliases);

    /// <summary>
    ///   Removes aliases from a channel. An alias the channel does not have is
    ///   ignored.
    /// </summary>
    /// <param name="channel">
    ///   The channel to remove the aliases from.
    /// </param>
    /// <param name="aliases">
    ///   The aliases to remove.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="channel"/> or <paramref name="aliases"/> is
    ///   <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <paramref name="channel"/> is not registered.
    /// </exception>
    /// <returns>
    ///   The updated channel.
    /// </returns>
    IAiringChannel RemoveChannelAliases(IAiringChannel channel, IEnumerable<string> aliases);

    #endregion

    #region Time Zones

    /// <summary>
    ///   Gets the time zones a schedule may use, normalised to IANA ids on
    ///   every host.
    /// </summary>
    /// <returns>
    ///   The available time zones.
    /// </returns>
    IReadOnlyList<TimeZoneInfo> GetAvailableTimeZones();

    /// <summary>
    ///   Tries to resolve a time zone from an IANA id, a Windows id, or a fixed
    ///   <c>"±HH:MM"</c> offset, building a custom zone for the last shape.
    /// </summary>
    /// <param name="idOrOffset">
    ///   The time zone id or fixed offset to resolve.
    /// </param>
    /// <param name="zone">
    ///   The resolved time zone, or <c>null</c> if it could not be resolved.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="idOrOffset"/> is <c>null</c>.
    /// </exception>
    /// <returns>
    ///   <c>true</c> if the time zone was resolved, otherwise <c>false</c>.
    /// </returns>
    bool TryGetTimeZone(string idOrOffset, [NotNullWhen(true)] out TimeZoneInfo? zone);

    #endregion

    #region Schedules

    /// <summary>
    ///   Event raised when an airing schedule is added, updated or removed.
    /// </summary>
    event EventHandler<AiringScheduleEventArgs>? ScheduleUpdated;

    /// <summary>
    ///   Adds or updates the schedule identified by the provider, series,
    ///   season and key, updating everything mutable on an existing one.
    /// </summary>
    /// <param name="provider">
    ///   The provider that owns the schedule.
    /// </param>
    /// <param name="data">
    ///   The whole schedule.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="provider"/> or <paramref name="data"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   Parts have not been added yet.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <paramref name="provider"/> is not the registered instance or does not
    ///   own the schedule, the schedule has no tracks, a track is of a kind the
    ///   provider does not declare, the channel is not registered, the identity
    ///   already exists on another channel, or the episode range starts after
    ///   it ends.
    /// </exception>
    /// <exception cref="TimeZoneNotFoundException">
    ///   The time zone cannot be normalised to an IANA id or a fixed offset.
    /// </exception>
    /// <returns>
    ///   The enriched schedule.
    /// </returns>
    IAiringSchedule AddOrUpdateSchedule(IAiringScheduleProvider provider, AiringScheduleData data);

    /// <summary>
    ///   Updates single fields on an existing schedule, leaving the rest alone.
    /// </summary>
    /// <param name="provider">
    ///   The provider that owns the schedule.
    /// </param>
    /// <param name="schedule">
    ///   The schedule to update.
    /// </param>
    /// <param name="data">
    ///   The fields to update.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="provider"/>, <paramref name="schedule"/> or
    ///   <paramref name="data"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   Parts have not been added yet.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <paramref name="provider"/> is not the registered instance or does not
    ///   own the schedule, the update leaves it without tracks, a track is of a
    ///   kind the provider does not declare, or the episode range starts after
    ///   it ends.
    /// </exception>
    /// <exception cref="TimeZoneNotFoundException">
    ///   The time zone cannot be normalised to an IANA id or a fixed offset.
    /// </exception>
    /// <returns>
    ///   The enriched schedule.
    /// </returns>
    IAiringSchedule UpdateSchedule(IAiringScheduleProvider provider, IAiringSchedule schedule, AiringScheduleUpdateData data);

    /// <summary>
    ///   Removes a schedule and its airings outright. An explicit removal is
    ///   not a hiatus.
    /// </summary>
    /// <param name="provider">
    ///   The provider that owns the schedule.
    /// </param>
    /// <param name="schedule">
    ///   The schedule to remove.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="provider"/> or <paramref name="schedule"/> is
    ///   <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   Parts have not been added yet.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <paramref name="provider"/> is not the registered instance or does not
    ///   own the schedule.
    /// </exception>
    /// <returns>
    ///   <c>true</c> if the schedule was removed, otherwise <c>false</c>.
    /// </returns>
    bool RemoveSchedule(IAiringScheduleProvider provider, IAiringSchedule schedule);

    /// <summary>
    ///   Removes every schedule the provider owns for the series, with their
    ///   airings. A purge helper for a provider dropping an entity.
    /// </summary>
    /// <param name="provider">
    ///   The provider that owns the schedules.
    /// </param>
    /// <param name="series">
    ///   The series to remove the schedules for.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="provider"/> or <paramref name="series"/> is
    ///   <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   Parts have not been added yet.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <paramref name="provider"/> is not the registered instance.
    /// </exception>
    /// <returns>
    ///   How many schedules were removed.
    /// </returns>
    int RemoveSchedulesForSeries(IAiringScheduleProvider provider, ISeries series);

    /// <summary>
    ///   Gets the schedule with the given ID.
    /// </summary>
    /// <param name="scheduleID">
    ///   The ID of the schedule.
    /// </param>
    /// <returns>
    ///   The schedule, or <c>null</c> if none could be found.
    /// </returns>
    IAiringSchedule? GetScheduleByID(Guid scheduleID);

    /// <summary>
    ///   Gets every provider's schedules for the series.
    /// </summary>
    /// <param name="series">
    ///   The series to get the schedules for.
    /// </param>
    /// <param name="options">
    ///   Optional. How to filter the schedules.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="series"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   Parts have not been added yet.
    /// </exception>
    /// <returns>
    ///   The schedules.
    /// </returns>
    IReadOnlyList<IAiringSchedule> GetSchedulesForSeries(ISeries series, AiringScheduleFilteringOptions? options = null);

    /// <summary>
    ///   Gets every provider's schedules narrowed to the season.
    /// </summary>
    /// <param name="season">
    ///   The season to get the schedules for.
    /// </param>
    /// <param name="options">
    ///   Optional. How to filter the schedules.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="season"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   Parts have not been added yet.
    /// </exception>
    /// <returns>
    ///   The schedules.
    /// </returns>
    IReadOnlyList<IAiringSchedule> GetSchedulesForSeason(ISeason season, AiringScheduleFilteringOptions? options = null);

    /// <summary>
    ///   Gets every schedule owned by the provider.
    /// </summary>
    /// <param name="providerID">
    ///   The ID of the provider.
    /// </param>
    /// <param name="options">
    ///   Optional. How to filter the schedules.
    /// </param>
    /// <exception cref="InvalidOperationException">
    ///   Parts have not been added yet.
    /// </exception>
    /// <returns>
    ///   The schedules.
    /// </returns>
    IReadOnlyList<IAiringSchedule> GetSchedulesForProvider(Guid providerID, AiringScheduleFilteringOptions? options = null);

    /// <summary>
    ///   Gets every schedule on the channel.
    /// </summary>
    /// <param name="channelID">
    ///   The ID of the channel.
    /// </param>
    /// <param name="options">
    ///   Optional. How to filter the schedules.
    /// </param>
    /// <exception cref="InvalidOperationException">
    ///   Parts have not been added yet.
    /// </exception>
    /// <returns>
    ///   The schedules.
    /// </returns>
    IReadOnlyList<IAiringSchedule> GetSchedulesForChannel(Guid channelID, AiringScheduleFilteringOptions? options = null);

    #endregion

    #region Episode Airings

    /// <summary>
    ///   Event raised once per write, carrying what the write added, updated
    ///   and withdrew on one schedule as three separate lists. An airing a
    ///   write left exactly as it was is in none of them.
    /// </summary>
    event EventHandler<EpisodeAiringsUpdatedEventArgs>? AiringsUpdated;

    /// <summary>
    ///   Replaces a schedule's airings, running delay inference over the whole
    ///   line unless it is turned off. This is how providers normally write.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     The submission is the schedule's whole line: an airing left out of it
    ///     is removed, which is what lets the inference tell a hiatus from
    ///     history. A provider that only ever sees part of a run at a time wants
    ///     <see cref="MergeAirings"/>, where silence means the opposite.
    ///   </para>
    ///   <para>
    ///     Absence is a hiatus and an explicit removal deletes. This is the
    ///     absence half: an airing left out whose slot is still ahead of us is
    ///     kept, slotless, with the slot it lost, and one whose slot has passed,
    ///     or that falls outside the run, is deleted as history.
    ///     <see cref="EpisodeAiringUpdateOptions.InferDelays"/> turned off is
    ///     the one thing that changes it, and it then deletes the lot.
    ///   </para>
    /// </remarks>
    /// <param name="provider">
    ///   The provider that owns the schedule.
    /// </param>
    /// <param name="schedule">
    ///   The schedule to write the airings to.
    /// </param>
    /// <param name="airings">
    ///   The airings, one per episode the provider knows a slot for.
    /// </param>
    /// <param name="options">
    ///   Optional. How to write the airings.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="provider"/>, <paramref name="schedule"/> or
    ///   <paramref name="airings"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   Parts have not been added yet.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <paramref name="provider"/> is not the registered instance or does not
    ///   own the schedule.
    /// </exception>
    /// <exception cref="AiringScheduleValidationException">
    ///   An episode falls outside the schedule's series, season or coverage,
    ///   two airings share a key, or the write would leave the schedule with no
    ///   airing inside the retention window while automatic cleanup is on.
    /// </exception>
    /// <returns>
    ///   The enriched airings on the schedule afterwards.
    /// </returns>
    IReadOnlyList<IEpisodeAiring> SetAirings(
        IAiringScheduleProvider provider,
        IAiringSchedule schedule,
        IEnumerable<EpisodeAiringData> airings,
        EpisodeAiringUpdateOptions? options = null
    );

    /// <summary>
    ///   Applies a delta to a schedule's airings: the airings in
    ///   <paramref name="airings"/> are added or updated, the airings in
    ///   <paramref name="removals"/> are deleted, and every other airing on
    ///   the schedule is left exactly as it is. It runs the same delay
    ///   inference <see cref="SetAirings"/> does, over the same whole line, and
    ///   raises the same event.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     This is the write for a provider that learns about a run a piece at
    ///     a time and cannot resubmit the whole of it. The one difference from
    ///     <see cref="SetAirings"/> is what silence means: there, an airing left
    ///     out of the call is removed, and here it is untouched. Say what went
    ///     through <paramref name="removals"/>.
    ///   </para>
    ///   <para>
    ///     Explicit removal deletes; absence is a hiatus. Naming an airing in
    ///     <paramref name="removals"/> is the same signal
    ///     <see cref="RemoveAiring"/> carries, so it is deleted whichever side
    ///     of now its slot is. A provider whose removal means "my source
    ///     pre-empted this" rather than "this row should go" asks for the
    ///     judgement an omission gets with
    ///     <see cref="EpisodeAiringUpdateOptions.KeepRemovalsAsHiatus"/>: a
    ///     removed airing whose slot is still ahead is then kept, slotless, as
    ///     a hiatus, while one whose slot has passed, or that falls outside the
    ///     run, is still deleted as history.
    ///   </para>
    ///   <para>
    ///     The write never changes what the schedule covers. The coverage on
    ///     <see cref="EpisodeAiringUpdateOptions"/> only states what this write
    ///     judges a removal against, and the schedule's own value is read for
    ///     anything left alone.
    ///   </para>
    /// </remarks>
    /// <param name="provider">
    ///   The provider that owns the schedule.
    /// </param>
    /// <param name="schedule">
    ///   The schedule to write the airings to.
    /// </param>
    /// <param name="airings">
    ///   The airings to add or update, one per episode this write knows a slot
    ///   for.
    /// </param>
    /// <param name="removals">
    ///   Optional. The airings on the schedule to take away. They are deleted
    ///   unless
    ///   <see cref="EpisodeAiringUpdateOptions.KeepRemovalsAsHiatus"/> asks for
    ///   the hiatus judgement an omission gets.
    /// </param>
    /// <param name="options">
    ///   Optional. How to write the airings.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="provider"/>, <paramref name="schedule"/> or
    ///   <paramref name="airings"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   Parts have not been added yet.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <paramref name="provider"/> is not the registered instance or does not
    ///   own the schedule, or a removal is estimated, unknown, or on another
    ///   schedule.
    /// </exception>
    /// <exception cref="AiringScheduleValidationException">
    ///   An episode falls outside the schedule's series, season or coverage,
    ///   two airings share a key, an airing is both submitted and removed, or
    ///   the write would leave the schedule with no airing inside the retention
    ///   window while automatic cleanup is on. A removal kept as a hiatus still
    ///   counts towards that window, since it holds on to the slot it lost, and
    ///   a deleted one does not.
    /// </exception>
    /// <returns>
    ///   The enriched airings this write wrote: everything in
    ///   <paramref name="airings"/>, plus any airing it could not leave alone,
    ///   such as a removal kept as a hiatus.
    /// </returns>
    IReadOnlyList<IEpisodeAiring> MergeAirings(
        IAiringScheduleProvider provider,
        IAiringSchedule schedule,
        IEnumerable<EpisodeAiringData> airings,
        IEnumerable<IEpisodeAiring>? removals = null,
        EpisodeAiringUpdateOptions? options = null
    );

    /// <summary>
    ///   Adds or updates a single airing on the schedule. It records the
    ///   original slot on a move of 24 hours or more and takes
    ///   <see cref="EpisodeAiringData.IsDelayed"/> as given, but runs no cause
    ///   detection or hiatus inference.
    /// </summary>
    /// <param name="provider">
    ///   The provider that owns the schedule.
    /// </param>
    /// <param name="schedule">
    ///   The schedule to write the airing to.
    /// </param>
    /// <param name="airing">
    ///   The airing.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="provider"/>, <paramref name="schedule"/> or
    ///   <paramref name="airing"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   Parts have not been added yet.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <paramref name="provider"/> is not the registered instance or does not
    ///   own the schedule.
    /// </exception>
    /// <exception cref="AiringScheduleValidationException">
    ///   The episode falls outside the schedule's series, season or coverage,
    ///   or the write would leave the schedule with no airing inside the
    ///   retention window while automatic cleanup is on.
    /// </exception>
    /// <returns>
    ///   The enriched airing.
    /// </returns>
    IEpisodeAiring AddOrUpdateAiring(IAiringScheduleProvider provider, IAiringSchedule schedule, EpisodeAiringData airing);

    /// <summary>
    ///   Updates single fields on an existing airing, leaving the rest alone.
    /// </summary>
    /// <param name="provider">
    ///   The provider that owns the airing.
    /// </param>
    /// <param name="airing">
    ///   The airing to update.
    /// </param>
    /// <param name="data">
    ///   The fields to update.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="provider"/>, <paramref name="airing"/> or
    ///   <paramref name="data"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   Parts have not been added yet.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <paramref name="provider"/> is not the registered instance or does not
    ///   own the airing.
    /// </exception>
    /// <exception cref="AiringScheduleValidationException">
    ///   The change would leave the schedule with no airing inside the
    ///   retention window while automatic cleanup is on.
    /// </exception>
    /// <returns>
    ///   The enriched airing.
    /// </returns>
    IEpisodeAiring UpdateAiring(IAiringScheduleProvider provider, IEpisodeAiring airing, EpisodeAiringUpdateData data);

    /// <summary>
    ///   Removes an airing outright. An explicit removal is not a hiatus.
    /// </summary>
    /// <remarks>
    ///   Naming an airing in <see cref="MergeAirings"/>'s removals does the
    ///   same thing, so reach for this one when there is nothing else to write.
    ///   Neither of them is what a source pre-empting an episode looks like:
    ///   that is an airing left out of a <see cref="SetAirings"/> submission,
    ///   or a removal a write asked to keep with
    ///   <see cref="EpisodeAiringUpdateOptions.KeepRemovalsAsHiatus"/>.
    /// </remarks>
    /// <param name="provider">
    ///   The provider that owns the airing.
    /// </param>
    /// <param name="airing">
    ///   The airing to remove.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="provider"/> or <paramref name="airing"/> is
    ///   <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   Parts have not been added yet.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <paramref name="provider"/> is not the registered instance or does not
    ///   own the airing.
    /// </exception>
    /// <returns>
    ///   <c>true</c> if the airing was removed, otherwise <c>false</c>.
    /// </returns>
    bool RemoveAiring(IAiringScheduleProvider provider, IEpisodeAiring airing);

    /// <summary>
    ///   Gets the airing with the given ID.
    /// </summary>
    /// <remarks>
    ///   An estimate resolves here as well as a stored airing does, since a
    ///   caller handed an ID has no way of telling the two apart. An estimate
    ///   is recomputed on every read, so its ID stops resolving once the
    ///   schedule no longer makes it.
    /// </remarks>
    /// <param name="airingID">
    ///   The ID of the airing.
    /// </param>
    /// <exception cref="InvalidOperationException">
    ///   Parts have not been added yet.
    /// </exception>
    /// <returns>
    ///   The airing, or <c>null</c> if none could be found.
    /// </returns>
    IEpisodeAiring? GetAiringByID(Guid airingID);

    /// <summary>
    ///   Gets the airings on the schedule.
    /// </summary>
    /// <param name="scheduleID">
    ///   The ID of the schedule.
    /// </param>
    /// <param name="options">
    ///   Optional. How to filter and order the airings.
    /// </param>
    /// <exception cref="InvalidOperationException">
    ///   Parts have not been added yet.
    /// </exception>
    /// <returns>
    ///   The airings.
    /// </returns>
    IReadOnlyList<IEpisodeAiring> GetAiringsForSchedule(Guid scheduleID, EpisodeAiringFilteringOptions? options = null);

    /// <summary>
    ///   Gets the airings for every episode of the series.
    /// </summary>
    /// <param name="series">
    ///   The series to get the airings for.
    /// </param>
    /// <param name="options">
    ///   Optional. How to filter and order the airings.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="series"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   Parts have not been added yet.
    /// </exception>
    /// <returns>
    ///   The airings.
    /// </returns>
    IReadOnlyList<IEpisodeAiring> GetAiringsForSeries(ISeries series, EpisodeAiringFilteringOptions? options = null);

    /// <summary>
    ///   Gets the airings for every episode of the season.
    /// </summary>
    /// <param name="season">
    ///   The season to get the airings for.
    /// </param>
    /// <param name="options">
    ///   Optional. How to filter and order the airings.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="season"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   Parts have not been added yet.
    /// </exception>
    /// <returns>
    ///   The airings.
    /// </returns>
    IReadOnlyList<IEpisodeAiring> GetAiringsForSeason(ISeason season, EpisodeAiringFilteringOptions? options = null);

    /// <summary>
    ///   Gets the airings for the episode, one per schedule that has or can
    ///   estimate a slot for it.
    /// </summary>
    /// <param name="episode">
    ///   The episode to get the airings for.
    /// </param>
    /// <param name="options">
    ///   Optional. How to filter and order the airings.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="episode"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   Parts have not been added yet.
    /// </exception>
    /// <returns>
    ///   The airings.
    /// </returns>
    IReadOnlyList<IEpisodeAiring> GetAiringsForEpisode(IEpisode episode, EpisodeAiringFilteringOptions? options = null);

    /// <summary>
    ///   Gets the one airing for the episode that best matches the preference:
    ///   the track preference, then the channel preference, then real before
    ///   estimated, then time.
    /// </summary>
    /// <param name="episode">
    ///   The episode to get the airing for.
    /// </param>
    /// <param name="options">
    ///   Optional. How to filter and order the airings.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="episode"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   Parts have not been added yet.
    /// </exception>
    /// <returns>
    ///   The airing, or <c>null</c> if the episode has none.
    /// </returns>
    IEpisodeAiring? GetAiringForEpisode(IEpisode episode, EpisodeAiringFilteringOptions? options = null);

    /// <summary>
    ///   Gets the airings within the range, by
    ///   <see cref="IEpisodeAiring.AiredAt"/> and, unless turned off, by
    ///   <see cref="IEpisodeAiring.OriginalAiredAt"/> for delayed airings, so a
    ///   week an episode was delayed out of still has something to draw its gap
    ///   from.
    /// </summary>
    /// <remarks>
    ///   This is the <em>pull</em> side of the same filtering
    ///   <see cref="SubscribeToAirings"/> pushes: it takes the same
    ///   <see cref="EpisodeAiringFilteringOptions"/> and answers the same
    ///   airings, so a consumer that would rather poll a window than subscribe
    ///   has no reason to re-implement any of it on top. It is also how a
    ///   subscriber reads back the gap left by downtime, since nothing is
    ///   replayed.
    /// </remarks>
    /// <param name="fromUtc">
    ///   The inclusive start of the range, in UTC.
    /// </param>
    /// <param name="toUtc">
    ///   The exclusive end of the range, in UTC.
    /// </param>
    /// <param name="options">
    ///   Optional. How to filter and order the airings.
    /// </param>
    /// <exception cref="InvalidOperationException">
    ///   Parts have not been added yet.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <paramref name="toUtc"/> is before <paramref name="fromUtc"/>.
    /// </exception>
    /// <returns>
    ///   The airings.
    /// </returns>
    IReadOnlyList<IEpisodeAiring> GetAiringsInRange(DateTime fromUtc, DateTime toUtc, EpisodeAiringFilteringOptions? options = null);

    #endregion

    #region Episode Airings | Links

    /// <summary>
    ///   Links airings of the same schedule together, so a double-episode slot
    ///   or a season released at once is one unit. Linking is deterministic:
    ///   linking either way around, or merging two sets in any order, ends in
    ///   the same state.
    /// </summary>
    /// <param name="provider">
    ///   The provider that owns the airings.
    /// </param>
    /// <param name="airings">
    ///   The airings to link, at least two, all on one schedule.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="provider"/> or <paramref name="airings"/> is
    ///   <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   Parts have not been added yet.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <paramref name="provider"/> is not the registered instance or does not
    ///   own the airings, fewer than two airings were given, or they come from
    ///   more than one schedule.
    /// </exception>
    /// <returns>
    ///   The enriched airings of the resulting link set.
    /// </returns>
    IReadOnlyList<IEpisodeAiring> LinkAirings(IAiringScheduleProvider provider, IEnumerable<IEpisodeAiring> airings);

    /// <summary>
    ///   Removes an airing from its link set, dissolving a set left with one
    ///   member.
    /// </summary>
    /// <param name="provider">
    ///   The provider that owns the airing.
    /// </param>
    /// <param name="airing">
    ///   The airing to unlink.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="provider"/> or <paramref name="airing"/> is
    ///   <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   Parts have not been added yet.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <paramref name="provider"/> is not the registered instance or does not
    ///   own the airing.
    /// </exception>
    /// <returns>
    ///   The enriched airing.
    /// </returns>
    IEpisodeAiring UnlinkAiring(IAiringScheduleProvider provider, IEpisodeAiring airing);

    /// <summary>
    ///   Gets every airing in the same link set as the given airing, itself
    ///   included. Hidden members drop out of the set.
    /// </summary>
    /// <param name="airingID">
    ///   The ID of the airing.
    /// </param>
    /// <exception cref="InvalidOperationException">
    ///   Parts have not been added yet.
    /// </exception>
    /// <returns>
    ///   The airings, which is empty when the airing is unknown or not linked.
    /// </returns>
    IReadOnlyList<IEpisodeAiring> GetLinkedAirings(Guid airingID);

    #endregion

    #region Airing Notifications

    /// <summary>
    ///   Subscribes to the airings as their slots pass, so a consumer can react
    ///   to an episode airing in real time instead of polling
    ///   <see cref="GetAiringsInRange"/>. Dispose the returned handle to
    ///   unsubscribe.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     This is a subscription rather than a plain event for two reasons a
    ///     <c>+=</c> cannot serve. Each subscriber brings <b>its own filters</b>,
    ///     applied to its own dispatch, so a consumer interested only in
    ///     TOKYO MX never sees the rest of a Tuesday. And because the service
    ///     knows what every live subscriber asked for, it can <b>do less work</b>:
    ///     the lookahead it keeps in memory is built from the union of the
    ///     current subscriptions, and with nobody subscribed it is not built at
    ///     all. That matters most for
    ///     <see cref="EpisodeAiringFilteringOptions.IncludeEstimates"/>, since
    ///     estimates are computed through the read path rather than stored: if
    ///     no live subscriber wants them, none are computed.
    ///   </para>
    ///   <para>
    ///     The handler is called <b>once per minute that has something for it</b>,
    ///     with everything that aired in that minute as one list — a simulcast
    ///     on two stations is one call carrying two airings, not two calls. See
    ///     <see cref="EpisodeAiredEventArgs"/> for how to collapse that list per
    ///     episode, per slot or per channel.
    ///   </para>
    ///   <para>
    ///     <b>Handlers run on the ticker's own thread</b>, one after another, so
    ///     a handler that blocks holds up the minute and every subscriber behind
    ///     it. Enqueue the real work through <c>IQueueScheduler</c> and return.
    ///     A handler that throws is logged and stepped over, and never takes the
    ///     tick or another subscriber with it.
    ///   </para>
    ///   <para>
    ///     Nothing is replayed: neither what passed while the server was down,
    ///     nor what passed before this subscription existed.
    ///   </para>
    /// </remarks>
    /// <param name="handler">
    ///   Called as each minute with a matching airing passes.
    /// </param>
    /// <param name="options">
    ///   Optional. How to filter the airings dispatched to this subscriber.
    ///   <c>null</c> means everything the server can see. Ordering and
    ///   preference options are ignored — a dispatch is always in slot order —
    ///   and so is
    ///   <see cref="EpisodeAiringFilteringOptions.IncludeDelayedOriginalSlots"/>,
    ///   since a delay gap is not an episode airing.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="handler"/> is <c>null</c>.
    /// </exception>
    /// <returns>
    ///   A handle that unsubscribes when disposed. Disposing it more than once,
    ///   or from more than one thread, is safe and does nothing the second time.
    /// </returns>
    IDisposable SubscribeToAirings(Action<EpisodeAiredEventArgs> handler, EpisodeAiringFilteringOptions? options = null);

    #endregion

    #region Refreshing

    /// <summary>
    ///   Hints that the series should be refreshed, enqueuing one job per
    ///   enabled provider and returning. Repeated hints for the same provider
    ///   and entity collapse into one.
    /// </summary>
    /// <param name="series">
    ///   The series to refresh.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="series"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   Parts have not been added yet.
    /// </exception>
    /// <returns>
    ///   A task that completes once the jobs are enqueued.
    /// </returns>
    Task ScheduleRefresh(ISeries series);

    /// <summary>
    ///   Hints that the season should be refreshed, enqueuing one job per
    ///   enabled provider and returning. Repeated hints for the same provider
    ///   and entity collapse into one.
    /// </summary>
    /// <param name="season">
    ///   The season to refresh.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="season"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   Parts have not been added yet.
    /// </exception>
    /// <returns>
    ///   A task that completes once the jobs are enqueued.
    /// </returns>
    Task ScheduleRefresh(ISeason season);

    /// <summary>
    ///   Hints that the episode should be refreshed, enqueuing one job per
    ///   enabled provider and returning. Repeated hints for the same provider
    ///   and entity collapse into one.
    /// </summary>
    /// <param name="episode">
    ///   The episode to refresh.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="episode"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   Parts have not been added yet.
    /// </exception>
    /// <returns>
    ///   A task that completes once the jobs are enqueued.
    /// </returns>
    Task ScheduleRefresh(IEpisode episode);

    /// <summary>
    ///   Refreshes the series through every enabled provider and waits for the
    ///   jobs to finish. A provider that throws is reported in the result
    ///   rather than thrown.
    /// </summary>
    /// <param name="series">
    ///   The series to refresh.
    /// </param>
    /// <param name="cancellationToken">
    ///   Optional. A cancellation token for cancelling the wait.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="series"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   Parts have not been added yet.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    ///   <paramref name="cancellationToken"/> was cancelled.
    /// </exception>
    /// <returns>
    ///   What each provider did, and the series' schedules afterwards.
    /// </returns>
    Task<AiringScheduleRefreshResult> RefreshAsync(ISeries series, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Refreshes the season through every enabled provider and waits for the
    ///   jobs to finish. A provider that throws is reported in the result
    ///   rather than thrown.
    /// </summary>
    /// <param name="season">
    ///   The season to refresh.
    /// </param>
    /// <param name="cancellationToken">
    ///   Optional. A cancellation token for cancelling the wait.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="season"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   Parts have not been added yet.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    ///   <paramref name="cancellationToken"/> was cancelled.
    /// </exception>
    /// <returns>
    ///   What each provider did, and the season's schedules afterwards.
    /// </returns>
    Task<AiringScheduleRefreshResult> RefreshAsync(ISeason season, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Refreshes the episode through every enabled provider and waits for the
    ///   jobs to finish. A provider that throws is reported in the result
    ///   rather than thrown.
    /// </summary>
    /// <param name="episode">
    ///   The episode to refresh.
    /// </param>
    /// <param name="cancellationToken">
    ///   Optional. A cancellation token for cancelling the wait.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="episode"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   Parts have not been added yet.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    ///   <paramref name="cancellationToken"/> was cancelled.
    /// </exception>
    /// <returns>
    ///   What each provider did, and the episode's schedules afterwards.
    /// </returns>
    Task<AiringScheduleRefreshResult> RefreshAsync(IEpisode episode, CancellationToken cancellationToken = default);

    #endregion

    #region Invalidation

    /// <summary>
    ///   Drops the cached estimate profiles of every schedule covering the
    ///   series, for link changes made outside the service.
    /// </summary>
    /// <param name="series">
    ///   The series whose links changed.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="series"/> is <c>null</c>.
    /// </exception>
    void InvalidateForSeries(ISeries series);

    /// <summary>
    ///   Drops the cached estimate profiles of the schedules attached to the
    ///   season, and of the series' schedules covering it, for link changes
    ///   made outside the service.
    /// </summary>
    /// <param name="season">
    ///   The season whose links changed.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="season"/> is <c>null</c>.
    /// </exception>
    void InvalidateForSeason(ISeason season);

    /// <summary>
    ///   Drops the cached estimate profiles of every schedule covering the
    ///   episode, for link changes made outside the service.
    /// </summary>
    /// <param name="episode">
    ///   The episode whose links changed.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="episode"/> is <c>null</c>.
    /// </exception>
    void InvalidateForEpisode(IEpisode episode);

    #endregion

    #region Helpers

    /// <summary>
    ///   The namespace for airing channel identifiers.
    /// </summary>
    public static Guid ChannelIdentifierNamespace { get; } = UuidUtility.GetV5("AiringChannelIdentifierNamespace", UuidUtility.PublicUuidNamespaces.OID);

    /// <summary>
    ///   Get the ID for the given channel name and type, without registering
    ///   anything. Providers that spell a channel alike end up on one ID, while
    ///   the same name with another type stays a different channel.
    /// </summary>
    /// <param name="name">
    ///   The name of the channel.
    /// </param>
    /// <param name="type">
    ///   The type of the channel.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="name"/> is <c>null</c>.
    /// </exception>
    /// <returns>
    ///   The ID of the channel.
    /// </returns>
    public static Guid GetChannelID(string name, AiringChannelType type)
        => UuidUtility.GetV5($"ChannelType={type},Name={NormalizeChannelName(name)}", ChannelIdentifierNamespace);

    /// <summary>
    ///   Normalise a channel name for comparison: NFKC, runs of whitespace
    ///   collapsed to one space, trimmed, then lower-cased with the invariant
    ///   culture. The NFKC pass matters for the full-width names some sources
    ///   use, e.g. <c>ＴＯＫＹＯ　ＭＸ</c>.
    /// </summary>
    /// <param name="name">
    ///   The name of the channel.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="name"/> is <c>null</c>.
    /// </exception>
    /// <returns>
    ///   The normalised name, which is empty when the name is only whitespace.
    /// </returns>
    public static string NormalizeChannelName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return Regex.Replace(name.Normalize(NormalizationForm.FormKC), @"\s+", " ").Trim().ToLowerInvariant();
    }

    /// <summary>
    ///   Build the name of a regional channel, e.g. <c>Amazon (US)</c>. This is
    ///   the only supported way to name one, so spellings cannot drift. No
    ///   suffix means worldwide or unknown, and broadcast stations never get
    ///   one.
    /// </summary>
    /// <param name="brand">
    ///   The brand of the service, e.g. <c>Amazon</c>.
    /// </param>
    /// <param name="countryCode">
    ///   The region the service is for, as an ISO 3166-1 alpha-2 code.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="brand"/> or <paramref name="countryCode"/> is
    ///   <c>null</c>.
    /// </exception>
    /// <returns>
    ///   The name of the regional channel.
    /// </returns>
    public static string GetRegionalChannelName(string brand, string countryCode)
    {
        ArgumentNullException.ThrowIfNull(brand);
        ArgumentNullException.ThrowIfNull(countryCode);

        return $"{brand.Trim()} ({countryCode.Trim().ToUpperInvariant()})";
    }

    #endregion
}
