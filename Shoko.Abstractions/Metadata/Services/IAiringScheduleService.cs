using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Config;
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
    #region Configuration

    /// <summary>
    ///   The service's own configuration: the preference lists, the cleanup
    ///   and the sweep budget. A client edits it through the configuration
    ///   service by this info, without knowing the server's settings type.
    /// </summary>
    ConfigurationInfo ConfigurationInfo { get; }

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
    ///   Event raised when a channel is registered, hidden or shown, when its
    ///   aliases change, for both sides of a merge, and when it takes a
    ///   country: the IDs a change took away as removed, then the channel kept
    ///   as added or updated. <see cref="AiringChannelEventArgs.Kind"/> tells
    ///   which.
    /// </summary>
    event EventHandler<AiringChannelEventArgs>? ChannelRegistered;

    /// <summary>
    ///   Gets the channel a provider names, registering it only if nothing
    ///   answers to the name. Own names are matched first, then aliases, among
    ///   the channels of the same type and country, so a provider naming a
    ///   merged channel is handed the channel it was merged into.
    /// </summary>
    /// <remarks>
    ///   With a country and no answer in it, the one channel of the type
    ///   without a country answering to the name is handed out, as
    ///   <see cref="GetChannelByName"/> finds it. A TV station found that way
    ///   takes the country and a new ID, or is merged into the channel in that
    ///   country answering to its own name.
    /// </remarks>
    /// <param name="name">
    ///   The display name of the channel, which is kept as given when the
    ///   channel is new. It never carries the country.
    /// </param>
    /// <param name="type">
    ///   The type of the channel, which is part of its identity.
    /// </param>
    /// <param name="countryCode">
    ///   Optional. The country the channel is for, as an ISO 3166-1 alpha-2
    ///   code, which is part of its identity. Pass it for a TV station, and for
    ///   a streaming service only when the service itself is regional.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="name"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   Parts have not been added yet.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <paramref name="name"/> is blank once normalised, or
    ///   <paramref name="countryCode"/> is not two letters.
    /// </exception>
    /// <returns>
    ///   The existing channel, or the newly registered one.
    /// </returns>
    IAiringChannel FindOrRegisterChannel(string name, AiringChannelType type, string? countryCode = null);

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
    ///   Gets the channel with the given name or alias, of the given type and
    ///   country. Own names are matched before aliases.
    /// </summary>
    /// <param name="nameOrAlias">
    ///   The name or alias of the channel.
    /// </param>
    /// <param name="type">
    ///   The type of the channel.
    /// </param>
    /// <param name="countryCode">
    ///   Optional. The country of the channel, as an ISO 3166-1 alpha-2 code.
    ///   <c>null</c> only matches channels with no country. With a country and
    ///   no match in it, the one channel without a country that matches is
    ///   returned, an own name before an alias, unless a channel in another
    ///   country has its own name.
    /// </param>
    /// <param name="useAliases">
    ///   If <c>false</c>, only own names are matched.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="nameOrAlias"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <paramref name="countryCode"/> is not two letters.
    /// </exception>
    /// <returns>
    ///   The channel, or <c>null</c> if none could be found.
    /// </returns>
    IAiringChannel? GetChannelByName(string nameOrAlias, AiringChannelType type, string? countryCode = null, bool useAliases = true);

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
    ///   The IDs of the channels the server hides, set through
    ///   <see cref="SetChannelHidden"/>. An airing read naming no channels
    ///   leaves their airings out, and one naming them in
    ///   <see cref="EpisodeAiringFilteringOptions.ChannelIDs"/> gets them.
    /// </summary>
    /// <remarks>
    ///   Read it when needed, since a channel can be hidden or shown at any
    ///   time.
    /// </remarks>
    IReadOnlySet<Guid> HiddenChannelIDs { get; }

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
    ///   the same type and country.
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

    /// <summary>
    ///   Replaces every alias of a channel with the given ones. Nothing is
    ///   changed when one of them conflicts. An alias equal to the channel's
    ///   own name, or a repeat, is ignored.
    /// </summary>
    /// <param name="channel">
    ///   The channel to set the aliases of.
    /// </param>
    /// <param name="aliases">
    ///   The full list of aliases. An empty list removes them all.
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
    ///   the same type and country.
    /// </exception>
    /// <returns>
    ///   The updated channel.
    /// </returns>
    IAiringChannel SetChannelAliases(IAiringChannel channel, IEnumerable<string> aliases);

    /// <summary>
    ///   Hides or shows a channel. An airing read naming no channels leaves
    ///   the airings of a hidden one out. Nothing is raised when the channel
    ///   already is as asked.
    /// </summary>
    /// <param name="channel">
    ///   The channel to hide or show.
    /// </param>
    /// <param name="hidden">
    ///   <c>true</c> to hide the channel, <c>false</c> to show it.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="channel"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <paramref name="channel"/> is not registered.
    /// </exception>
    /// <returns>
    ///   The updated channel.
    /// </returns>
    IAiringChannel SetChannelHidden(IAiringChannel channel, bool hidden);

    /// <summary>
    ///   Merges channels into another one of the same type. Their schedules
    ///   move to the target without changing any schedule or airing ID, their
    ///   names and aliases become the target's aliases, and they are deleted.
    ///   The target keeps its own country, but a TV station without one takes
    ///   the country every source with one agrees on, and with it a new ID.
    /// </summary>
    /// <remarks>
    ///   In the preferred channels the target takes the best position any of
    ///   them had, and it stays hidden or shown as it was. A name that
    ///   another channel of the target's type and country answers to is not
    ///   added as an alias.
    /// </remarks>
    /// <param name="target">
    ///   The channel to keep.
    /// </param>
    /// <param name="sources">
    ///   The channels to merge into it. Repeats collapse.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="target"/> or <paramref name="sources"/> is
    ///   <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   A channel is not registered, is the target itself, or has another
    ///   type than the target.
    /// </exception>
    /// <returns>
    ///   The merged channel.
    /// </returns>
    IAiringChannel MergeChannels(IAiringChannel target, IEnumerable<IAiringChannel> sources);

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
    ///   write left exactly as it was is in none of them. Also raised, once per
    ///   schedule, for the airings an added or removed episode or series, or a
    ///   changed link, makes resolve differently.
    /// </summary>
    event EventHandler<EpisodeAiringsUpdatedEventArgs>? AiringsUpdated;

    /// <summary>
    ///   How far back airings are kept, as configured, whether or not
    ///   automatic cleanup is on.
    /// </summary>
    /// <remarks>
    ///   Set in months, so the span shifts slightly with the length of the
    ///   months it covers. <see cref="RetentionCutoff"/> is when it applies.
    /// </remarks>
    TimeSpan Retention { get; }

    /// <summary>
    ///   The oldest a schedule's latest airing may be and still be kept, in
    ///   UTC, or <c>null</c> while automatic cleanup is off.
    /// </summary>
    /// <remarks>
    ///   A schedule whose every airing slots before this (by
    ///   <see cref="IEpisodeAiring.AiredAt"/>, else
    ///   <see cref="IEpisodeAiring.OriginalAiredAt"/>) is removed by the next
    ///   cleanup, and a write leaving one so is refused with an
    ///   <see cref="AiringScheduleValidationException"/> under <c>#schedule</c>.
    ///   It moves with the clock, so read it when needed.
    /// </remarks>
    DateTime? RetentionCutoff { get; }

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
    ///   An airing has neither an episode nor a sequence number, its sequence
    ///   number is below <c>1</c> or past the schedule's coverage, its episode
    ///   falls outside the schedule's series, season or coverage, two airings
    ///   share a key, or the write would leave the schedule with no airing
    ///   inside the retention window while automatic cleanup is on.
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
    ///     Explicit removal deletes; absence is a hiatus. An airing named in
    ///     <paramref name="removals"/> is deleted whichever side of now its
    ///     slot is. A provider whose removal means "my source pre-empted this"
    ///     rather than "this row should go" asks for the judgement an omission
    ///     gets with
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
    ///   An airing has neither an episode nor a sequence number, its sequence
    ///   number is below <c>1</c> or past the schedule's coverage, its episode
    ///   falls outside the schedule's series, season or coverage, two airings
    ///   share a key, an airing is both submitted and removed, or
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
    ///   Gets the airings for every episode of each of many series in one
    ///   read, such as the next airing of every anime of a season. Each series
    ///   answers what <see cref="GetAiringsForSeries(ISeries, EpisodeAiringFilteringOptions)"/>
    ///   would for it alone, while the lookups they share are made once.
    /// </summary>
    /// <remarks>
    ///   <see cref="EpisodeAiringFilteringOptions.NextOnly"/> reduces each
    ///   series on its own, so an empty
    ///   <see cref="EpisodeAiringFilteringOptions.NextPer"/> gives each series
    ///   its single next airing. A series listed twice is read once.
    /// </remarks>
    /// <param name="series">
    ///   The series to get the airings for.
    /// </param>
    /// <param name="options">
    ///   Optional. How to filter and order the airings of every series.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="series"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <paramref name="series"/> holds a <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   Parts have not been added yet.
    /// </exception>
    /// <returns>
    ///   The airings of each series by its <see cref="IMetadata.ID"/>, an
    ///   empty list for a series with none.
    /// </returns>
    IReadOnlyDictionary<MetadataGuid, IReadOnlyList<IEpisodeAiring>> GetAiringsForSeries(
        IEnumerable<ISeries> series,
        EpisodeAiringFilteringOptions? options = null
    );

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
    ///   from. They are ordered by the slot they occupy in the range.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     This is the <em>pull</em> side of the same filtering
    ///     <see cref="SubscribeToAirings"/> pushes: it takes the same
    ///     <see cref="EpisodeAiringFilteringOptions"/> and answers the same
    ///     airings, so a consumer that would rather poll a window than
    ///     subscribe has no reason to re-implement any of it on top. It is also
    ///     how a subscriber reads back the gap left by downtime, since nothing
    ///     is replayed.
    ///   </para>
    ///   <para>
    ///     The range is compared as instants. The offsets only matter to
    ///     date-only entries, which carry a calendar date rather than an
    ///     instant: one is in the range when its date falls between the
    ///     calendar dates of <paramref name="from"/> and of the last instant
    ///     before <paramref name="to"/>, each read in its own offset.
    ///   </para>
    /// </remarks>
    /// <param name="from">
    ///   The inclusive start of the range.
    /// </param>
    /// <param name="to">
    ///   The exclusive end of the range.
    /// </param>
    /// <param name="options">
    ///   Optional. How to filter and order the airings.
    /// </param>
    /// <exception cref="InvalidOperationException">
    ///   Parts have not been added yet.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <paramref name="to"/> is before <paramref name="from"/>.
    /// </exception>
    /// <returns>
    ///   The airings.
    /// </returns>
    IReadOnlyList<IEpisodeAiring> GetAiringsInRange(DateTimeOffset from, DateTimeOffset to, EpisodeAiringFilteringOptions? options = null);

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
    ///   Get the ID for the given channel name, type and country, without
    ///   registering anything. Providers that spell a channel alike end up on
    ///   one ID, while another type or country stays another channel.
    /// </summary>
    /// <param name="name">
    ///   The name of the channel.
    /// </param>
    /// <param name="type">
    ///   The type of the channel.
    /// </param>
    /// <param name="countryCode">
    ///   Optional. The country of the channel, as an ISO 3166-1 alpha-2 code.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="name"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <paramref name="countryCode"/> is not two letters.
    /// </exception>
    /// <returns>
    ///   The ID of the channel.
    /// </returns>
    public static Guid GetChannelID(string name, AiringChannelType type, string? countryCode = null)
        => NormalizeCountryCode(countryCode) is { } country
            ? UuidUtility.GetV5($"ChannelType={type},Name={NormalizeChannelName(name)},Country={country}", ChannelIdentifierNamespace)
            : UuidUtility.GetV5($"ChannelType={type},Name={NormalizeChannelName(name)}", ChannelIdentifierNamespace);

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
    ///   Normalise a channel's country code: trimmed and upper-cased, with a
    ///   blank code read as no country.
    /// </summary>
    /// <param name="countryCode">
    ///   The country code, as an ISO 3166-1 alpha-2 code in any casing.
    /// </param>
    /// <exception cref="ArgumentException">
    ///   <paramref name="countryCode"/> is not two ASCII letters.
    /// </exception>
    /// <returns>
    ///   The upper-case code, or <c>null</c> for no country.
    /// </returns>
    public static string? NormalizeCountryCode(string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
            return null;

        var code = countryCode.Trim().ToUpperInvariant();
        if (code.Length is not 2 || !char.IsAsciiLetterUpper(code[0]) || !char.IsAsciiLetterUpper(code[1]))
            throw new ArgumentException($"Invalid country code: '{countryCode}'. Use an ISO 3166-1 alpha-2 code.", nameof(countryCode));

        return code;
    }

    #endregion
}
