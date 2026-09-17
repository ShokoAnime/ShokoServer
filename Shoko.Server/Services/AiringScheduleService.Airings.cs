using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Models.Airing;
using Shoko.Server.Repositories;
using Shoko.Server.Services.Airing;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;
using Shoko.Server.Utilities.Airing;

#nullable enable
namespace Shoko.Server.Services;

public partial class AiringScheduleService
{
    #region Episode Airings | Writing

    /// <inheritdoc/>
    public IReadOnlyList<IEpisodeAiring> SetAirings(
        IAiringScheduleProvider provider,
        IAiringSchedule schedule,
        IEnumerable<EpisodeAiringData> airings,
        EpisodeAiringUpdateOptions? options = null
    )
    {
        ArgumentNullException.ThrowIfNull(airings);

        var info = GetRegisteredProvider(provider, nameof(provider));
        var row = GetOwnedSchedule(info, schedule, nameof(schedule));
        return WriteAirings(row, airings, null, options ?? new EpisodeAiringUpdateOptions());
    }

    /// <inheritdoc/>
    public IReadOnlyList<IEpisodeAiring> MergeAirings(
        IAiringScheduleProvider provider,
        IAiringSchedule schedule,
        IEnumerable<EpisodeAiringData> airings,
        IEnumerable<IEpisodeAiring>? removals = null,
        EpisodeAiringUpdateOptions? options = null
    )
    {
        ArgumentNullException.ThrowIfNull(airings);

        var info = GetRegisteredProvider(provider, nameof(provider));
        var row = GetOwnedSchedule(info, schedule, nameof(schedule));
        return WriteAirings(row, airings, ResolveRemovals(info, row, removals), options ?? new EpisodeAiringUpdateOptions());
    }

    /// <summary>
    /// The one write behind <see cref="SetAirings"/> and
    /// <see cref="MergeAirings"/>. The only thing that tells them apart is where
    /// the removal set comes from: a whole-line write derives it from what was
    /// left out, and a delta states it.
    /// </summary>
    /// <param name="row">The schedule to write to, already checked against its owner.</param>
    /// <param name="airings">The airings to add or update.</param>
    /// <param name="removalKeys">The keys of the airings to take away, or <see langword="null"/> to take away everything not submitted.</param>
    /// <param name="options">How to write the airings.</param>
    /// <returns>The enriched airings the write wrote.</returns>
    /// <exception cref="AiringScheduleValidationException">An airing is outside the schedule's series, season or coverage, shares a key with another, is both submitted and removed, or is older than the retention window.</exception>
    private IReadOnlyList<IEpisodeAiring> WriteAirings(
        AiringSchedule row,
        IEnumerable<EpisodeAiringData> airings,
        IReadOnlySet<string>? removalKeys,
        EpisodeAiringUpdateOptions options
    )
    {
        var now = DateTime.UtcNow;
        var context = new AiringReadContext(this, includeDisabled: true);
        var scheduleView = context.GetSchedule(row);
        var submissions = ValidateAirings(context, row, airings, now, removalKeys);
        var existingRows = RepoFactory.EpisodeAiring.GetByScheduleID(row.AiringScheduleID);
        var existingByKey = existingRows.ToDictionary(entry => entry.Key, StringComparer.Ordinal);
        var linkKeys = GetLinkKeys(existingRows);
        var existing = existingRows
            .Select(entry => new ExistingAiring
            {
                Key = entry.Key,
                EpisodeKey = AiringScheduleUtility.GetDerivedAiringKey(entry.EpisodeSource, entry.EpisodeID),
                EpisodeNumber = GetNormalEpisodeNumber(context, entry.EpisodeSource, entry.EpisodeID),
                AiredAt = entry.AiredAt,
                OriginalAiredAt = entry.OriginalAiredAt,
                IsDelayed = entry.IsDelayed,
                LinkKey = linkKeys.GetValueOrDefault(entry.EpisodeAiringID),
            })
            .ToList();
        var submitted = submissions
            .Select(entry => new SubmittedAiring
            {
                Key = entry.Key,
                EpisodeKey = AiringScheduleUtility.GetDerivedAiringKey(entry.Source, entry.ID),
                AiredAt = entry.Data.AiredAt,
                OriginalAiredAt = entry.Data.OriginalAiredAt,
                IsDelayed = entry.Data.IsDelayed,
            })
            .ToList();
        // Coverage is the schedule's own unless this write stated otherwise; a
        // stated value judges this write's removals and is never written back.
        var inferenceOptions = new AiringInferenceOptions
        {
            InferDelays = options.InferDelays,
            IsFinished = options.IsFinished ?? row.IsFinished,
            FirstEpisodeNumber = options.HasFirstEpisodeNumberSet ? options.FirstEpisodeNumber : row.FirstEpisodeNumber,
            LastEpisodeNumber = options.HasLastEpisodeNumberSet ? options.LastEpisodeNumber : row.LastEpisodeNumber,
            SupersededEpisodeKeys = GetSupersededEpisodeKeys(row, existingRows, now),
        };
        var result = removalKeys is null
            ? AiringScheduleUtility.InferAirings(existing, submitted, now, inferenceOptions)
            : AiringScheduleUtility.MergeAirings(existing, submitted, removalKeys, now, inferenceOptions);

        // Removals go first, so a re-keyed airing can't collide with a row that
        // is on its way out, and a promoted link head is picked from what lives.
        var deleted = new List<EpisodeAiring>();
        foreach (var airing in result.ToDelete)
        {
            if (!existingByKey.TryGetValue(airing.Key, out var entry))
                continue;

            ForgetAiringID(row, entry);
            existingByKey.Remove(airing.Key);
            RepoFactory.EpisodeAiring.Delete(entry);
            deleted.Add(entry);
        }

        var submissionsByKey = submissions.ToDictionary(entry => entry.Key, StringComparer.Ordinal);
        var renamedKeys = new Dictionary<string, string>(StringComparer.Ordinal);
        var saved = new List<EpisodeAiring>();
        var states = new AiringWriteState[result.ToSave.Count];
        for (var index = 0; index < result.ToSave.Count; index++)
        {
            var airing = result.ToSave[index];
            var entry = airing.ExistingKey is { } existingKey && existingByKey.TryGetValue(existingKey, out var match)
                ? match
                : new EpisodeAiring() { AiringScheduleID = row.AiringScheduleID, CreatedAt = now };
            var isNew = entry.EpisodeAiringID is 0;
            var before = GetRowState(entry);
            if (!isNew && !string.Equals(entry.Key, airing.Key, StringComparison.Ordinal))
            {
                ForgetAiringID(row, entry);
                renamedKeys[entry.Key] = airing.Key;
            }

            entry.Key = airing.Key;
            if (submissionsByKey.TryGetValue(airing.Key, out var submission))
            {
                entry.EpisodeSource = submission.Source;
                entry.EpisodeID = submission.ID;
                entry.Url = string.IsNullOrWhiteSpace(submission.Data.Url) ? null : submission.Data.Url.Trim();
            }

            entry.AiredAt = airing.AiredAt;
            entry.OriginalAiredAt = airing.OriginalAiredAt;
            entry.IsDelayed = airing.IsDelayed;
            // A row a write hands back exactly as it is stored is left alone,
            // timestamp and all, and reported in none of the three lists.
            var changed = isNew || GetRowState(entry) != before;
            if (changed)
            {
                entry.LastUpdatedAt = now;
                RepoFactory.EpisodeAiring.Save(entry);
                RememberAiringID(row, entry);
            }

            saved.Add(entry);
            states[index] = !changed
                ? AiringWriteState.Unchanged
                : isNew
                    ? AiringWriteState.Added
                    : airing.IsWithdrawn
                        ? AiringWriteState.Withdrawn
                        : AiringWriteState.Updated;
        }

        // Links are carried as keys through the inference, so they are re-pointed
        // once every row has its final key and local ID.
        var savedByKey = saved.ToDictionary(entry => entry.Key, StringComparer.Ordinal);
        for (var index = 0; index < result.ToSave.Count; index++)
        {
            var airing = result.ToSave[index];
            var entry = saved[index];
            var linkKey = airing.LinkKey is { } key ? renamedKeys.GetValueOrDefault(key, key) : null;
            var linkedToID = (int?)null;
            if (linkKey is not null)
            {
                // A head this write took away leaves the rest of its set pointing
                // at a row that is gone, which is exactly what NormalizeLinkSets
                // promotes the next smallest surviving member out of below.
                // Clearing the pointer here instead would dissolve a set of three
                // or more, since nothing would be left to regroup them on.
                if (!savedByKey.TryGetValue(linkKey, out var head))
                    continue;

                linkedToID = head.EpisodeAiringID;
            }

            if (entry.LinkedToID == linkedToID)
                continue;

            entry.LinkedToID = linkedToID;
            entry.LastUpdatedAt = now;
            RepoFactory.EpisodeAiring.Save(entry);
            if (states[index] is AiringWriteState.Unchanged)
                states[index] = AiringWriteState.Updated;
        }

        NormalizeLinkSets(row.AiringScheduleID, saved);
        _profiles.TryRemove(row.AiringScheduleID, out _);
        InvalidateProfilesForSeries(row.SeriesSource, row.SeriesID);

        // A schedule a provider emptied out, and that is past the buffer a
        // two-step write needs, goes with its last airing.
        if (saved.Count is 0 && RepoFactory.EpisodeAiring.GetByScheduleID(row.AiringScheduleID).Count is 0 && now - row.CreatedAt > EmptyScheduleBuffer)
        {
            RemoveSchedule(row);
            return [];
        }

        var added = new List<EpisodeAiring>();
        var updated = new List<EpisodeAiring>();
        var hiatus = new List<EpisodeAiring>();
        for (var index = 0; index < saved.Count; index++)
        {
            switch (states[index])
            {
                case AiringWriteState.Added:
                    added.Add(saved[index]);
                    break;
                case AiringWriteState.Updated:
                    updated.Add(saved[index]);
                    break;
                case AiringWriteState.Withdrawn:
                    hiatus.Add(saved[index]);
                    break;
            }
        }

        // A row kept as a hiatus and a row deleted as history are the same
        // signal to a consumer: the schedule no longer lists it.
        var withdrawn = hiatus.Concat(deleted).ToList();
        AiringsUpdated?.Invoke(this, new EpisodeAiringsUpdatedEventArgs
        {
            Reason = GetWriteReason(added.Count, updated.Count, withdrawn.Count),
            Schedule = scheduleView,
            Added = ToViews(context, scheduleView, added),
            Updated = ToViews(context, scheduleView, updated),
            Withdrawn = ToViews(context, scheduleView, withdrawn),
        });
        return ToViews(context, scheduleView, [.. added, .. updated, .. hiatus]);
    }

    /// <inheritdoc/>
    public IEpisodeAiring AddOrUpdateAiring(IAiringScheduleProvider provider, IAiringSchedule schedule, EpisodeAiringData airing)
    {
        ArgumentNullException.ThrowIfNull(airing);

        var info = GetRegisteredProvider(provider, nameof(provider));
        var row = GetOwnedSchedule(info, schedule, nameof(schedule));
        var now = DateTime.UtcNow;
        var context = new AiringReadContext(this, includeDisabled: true);
        var scheduleView = context.GetSchedule(row);
        var submission = ValidateAirings(context, row, [airing], now).Single();

        var entry = RepoFactory.EpisodeAiring.GetByScheduleIDAndKey(row.AiringScheduleID, submission.Key);
        var reason = UpdateReason.Added;
        if (entry is null)
        {
            entry = new EpisodeAiring()
            {
                AiringScheduleID = row.AiringScheduleID,
                Key = submission.Key,
                CreatedAt = now,
                OriginalAiredAt = airing.OriginalAiredAt,
                IsDelayed = airing.IsDelayed ?? false,
            };
        }
        else
        {
            reason = UpdateReason.Updated;
            // A move of a day or more keeps the first scheduled slot; cause
            // detection belongs to a whole-line write, so it doesn't run here.
            entry.OriginalAiredAt = airing.OriginalAiredAt
                ?? (HasMoved(entry.AiredAt, airing.AiredAt) ? entry.OriginalAiredAt ?? entry.AiredAt : entry.OriginalAiredAt);
            entry.IsDelayed = airing.IsDelayed ?? entry.IsDelayed;
        }

        entry.EpisodeSource = submission.Source;
        entry.EpisodeID = submission.ID;
        entry.Url = string.IsNullOrWhiteSpace(airing.Url) ? null : airing.Url.Trim();
        entry.AiredAt = airing.AiredAt;
        entry.LastUpdatedAt = now;
        RepoFactory.EpisodeAiring.Save(entry);
        RememberAiringID(row, entry);
        _profiles.TryRemove(row.AiringScheduleID, out _);
        InvalidateProfilesForSeries(row.SeriesSource, row.SeriesID);

        var views = new List<IEpisodeAiring>() { new EpisodeAiringView(context, scheduleView, entry) };
        AiringsUpdated?.Invoke(this, new EpisodeAiringsUpdatedEventArgs
        {
            Reason = reason,
            Schedule = scheduleView,
            Added = reason is UpdateReason.Added ? views : [],
            Updated = reason is UpdateReason.Added ? [] : views,
        });
        return views[0];
    }

    /// <inheritdoc/>
    public IEpisodeAiring UpdateAiring(IAiringScheduleProvider provider, IEpisodeAiring airing, EpisodeAiringUpdateData data)
    {
        ArgumentNullException.ThrowIfNull(airing);
        ArgumentNullException.ThrowIfNull(data);

        var info = GetRegisteredProvider(provider, nameof(provider));
        var (row, entry) = GetOwnedAiring(info, airing, nameof(airing));
        var now = DateTime.UtcNow;
        if (data.HasAiredAtSet)
        {
            if (!data.HasOriginalAiredAtSet && HasMoved(entry.AiredAt, data.AiredAt))
                entry.OriginalAiredAt ??= entry.AiredAt;

            entry.AiredAt = data.AiredAt;
        }

        if (data.HasOriginalAiredAtSet)
            entry.OriginalAiredAt = data.OriginalAiredAt;
        if (data.IsDelayed is { } isDelayed)
            entry.IsDelayed = isDelayed;
        if (data.HasUrlSet)
            entry.Url = string.IsNullOrWhiteSpace(data.Url) ? null : data.Url.Trim();

        ValidateRetention(entry.Key, entry.AiredAt ?? entry.OriginalAiredAt, now);
        entry.LastUpdatedAt = now;
        RepoFactory.EpisodeAiring.Save(entry);
        _profiles.TryRemove(row.AiringScheduleID, out _);
        InvalidateProfilesForSeries(row.SeriesSource, row.SeriesID);

        var context = new AiringReadContext(this, includeDisabled: true);
        var scheduleView = context.GetSchedule(row);
        var view = new EpisodeAiringView(context, scheduleView, entry);
        AiringsUpdated?.Invoke(this, new EpisodeAiringsUpdatedEventArgs { Reason = UpdateReason.Updated, Schedule = scheduleView, Updated = [view] });
        return view;
    }

    /// <inheritdoc/>
    public bool RemoveAiring(IAiringScheduleProvider provider, IEpisodeAiring airing)
    {
        ArgumentNullException.ThrowIfNull(airing);

        var info = GetRegisteredProvider(provider, nameof(provider));
        var (row, entry) = GetOwnedAiring(info, airing, nameof(airing));
        var context = new AiringReadContext(this, includeDisabled: true);
        var scheduleView = context.GetSchedule(row);
        var view = new EpisodeAiringView(context, scheduleView, entry);
        ForgetAiringID(row, entry);
        RepoFactory.EpisodeAiring.Delete(entry);
        NormalizeLinkSets(row.AiringScheduleID, []);
        _profiles.TryRemove(row.AiringScheduleID, out _);
        InvalidateProfilesForSeries(row.SeriesSource, row.SeriesID);

        AiringsUpdated?.Invoke(this, new EpisodeAiringsUpdatedEventArgs { Reason = UpdateReason.Removed, Schedule = scheduleView, Withdrawn = [view] });
        return true;
    }

    #endregion

    #region Episode Airings | Links

    /// <inheritdoc/>
    public IReadOnlyList<IEpisodeAiring> LinkAirings(IAiringScheduleProvider provider, IEnumerable<IEpisodeAiring> airings)
    {
        ArgumentNullException.ThrowIfNull(airings);

        var info = GetRegisteredProvider(provider, nameof(provider));
        // The same airing handed in twice is still one airing, and a set of one
        // is no link, so the count is taken after the duplicates are dropped.
        var wanted = airings.Where(airing => airing is not null).DistinctBy(airing => airing.ID).ToList();
        if (wanted.Count < 2)
            throw new ArgumentException("Linking needs at least two distinct airings.", nameof(airings));

        AiringSchedule? row = null;
        var entries = new List<EpisodeAiring>();
        foreach (var airing in wanted)
        {
            var (schedule, entry) = GetOwnedAiring(info, airing, nameof(airings));
            if (row is null)
                row = schedule;
            else if (row.AiringScheduleID != schedule.AiringScheduleID)
                throw new ArgumentException("Every airing in a link must be on the same schedule.", nameof(airings));

            entries.Add(entry);
        }

        // Merging two sets re-points every member straight at the new head, so a
        // read never follows a chain and the order the sets arrive in is moot.
        var members = entries
            .SelectMany(entry => entry.LinkedToID is { } linkedToID ? RepoFactory.EpisodeAiring.GetByLinkedToID(linkedToID) : [entry])
            .Concat(entries)
            .DistinctBy(entry => entry.EpisodeAiringID)
            .ToList();
        var head = members.MinBy(entry => entry.EpisodeAiringID)!;
        foreach (var entry in members)
        {
            entry.LinkedToID = head.EpisodeAiringID;
            entry.LastUpdatedAt = DateTime.UtcNow;
            RepoFactory.EpisodeAiring.Save(entry);
        }

        var context = new AiringReadContext(this, includeDisabled: true);
        var scheduleView = context.GetSchedule(row!);
        var views = members
            .OrderBy(entry => entry.EpisodeAiringID)
            .Select(IEpisodeAiring (entry) => new EpisodeAiringView(context, scheduleView, entry))
            .ToList();
        AiringsUpdated?.Invoke(this, new EpisodeAiringsUpdatedEventArgs { Reason = UpdateReason.Updated, Schedule = scheduleView, Updated = views });
        return views;
    }

    /// <inheritdoc/>
    public IEpisodeAiring UnlinkAiring(IAiringScheduleProvider provider, IEpisodeAiring airing)
    {
        ArgumentNullException.ThrowIfNull(airing);

        var info = GetRegisteredProvider(provider, nameof(provider));
        var (row, entry) = GetOwnedAiring(info, airing, nameof(airing));
        if (entry.LinkedToID is not null)
        {
            entry.LinkedToID = null;
            entry.LastUpdatedAt = DateTime.UtcNow;
            RepoFactory.EpisodeAiring.Save(entry);
            NormalizeLinkSets(row.AiringScheduleID, []);
        }

        var context = new AiringReadContext(this, includeDisabled: true);
        var scheduleView = context.GetSchedule(row);
        var view = new EpisodeAiringView(context, scheduleView, entry);
        AiringsUpdated?.Invoke(this, new EpisodeAiringsUpdatedEventArgs { Reason = UpdateReason.Updated, Schedule = scheduleView, Updated = [view] });
        return view;
    }

    /// <inheritdoc/>
    public IReadOnlyList<IEpisodeAiring> GetLinkedAirings(Guid airingID)
    {
        if (!_loaded)
            throw new InvalidOperationException("Parts have not been added yet.");

        if (GetAiringRow(airingID) is not { } entry || entry.LinkedToID is not { } linkedToID)
            return [];

        var context = new AiringReadContext(this);
        if (context.GetSchedule(entry.AiringScheduleID) is not { } scheduleView)
            return [];

        return RepoFactory.EpisodeAiring.GetByLinkedToID(linkedToID)
            .OrderBy(member => member.EpisodeAiringID)
            .Select(IEpisodeAiring (member) => new EpisodeAiringView(context, scheduleView, member))
            .ToList();
    }

    /// <summary>
    /// Point every member of every link set on a schedule at the smallest live
    /// member, and dissolve a set that is down to one. It runs after any write
    /// that could have taken a head away.
    /// </summary>
    /// <param name="scheduleID">The local ID of the schedule to normalise.</param>
    /// <param name="touched">Rows the caller has in hand, so their pointers are saved even when they are already right.</param>
    private static void NormalizeLinkSets(int scheduleID, IReadOnlyList<EpisodeAiring> touched)
    {
        var rows = RepoFactory.EpisodeAiring.GetByScheduleID(scheduleID)
            .Concat(touched)
            .DistinctBy(entry => entry.EpisodeAiringID)
            .ToList();
        foreach (var group in rows.Where(entry => entry.LinkedToID is not null).GroupBy(entry => entry.LinkedToID!.Value))
        {
            var members = group.ToList();
            var head = members.Count > 1 ? members.Min(member => member.EpisodeAiringID) : (int?)null;
            foreach (var member in members)
            {
                if (member.LinkedToID == head)
                    continue;

                member.LinkedToID = head;
                RepoFactory.EpisodeAiring.Save(member);
            }
        }
    }

    /// <summary>
    /// The key of every airing's link head, which is how the inference carries
    /// a link set through a write that may re-key its members.
    /// </summary>
    /// <param name="rows">The schedule's airings.</param>
    /// <returns>The head's key by airing ID, for the linked airings only.</returns>
    private static Dictionary<int, string> GetLinkKeys(IReadOnlyList<EpisodeAiring> rows)
    {
        var keysByID = rows.ToDictionary(entry => entry.EpisodeAiringID, entry => entry.Key);
        return rows
            .Where(entry => entry.LinkedToID is { } linkedToID && keysByID.ContainsKey(linkedToID))
            .ToDictionary(entry => entry.EpisodeAiringID, entry => keysByID[entry.LinkedToID!.Value]);
    }

    #endregion

    #region Episode Airings | Reading

    /// <inheritdoc/>
    public IEpisodeAiring? GetAiringByID(Guid airingID)
    {
        if (!_loaded)
            throw new InvalidOperationException("Parts have not been added yet.");

        var context = new AiringReadContext(this, includeDisabled: true);
        if (GetAiringRow(airingID) is not { } entry)
            return GetEstimateByID(context, airingID);

        return context.GetSchedule(entry.AiringScheduleID) is { } scheduleView
            ? new EpisodeAiringView(context, scheduleView, entry)
            : null;
    }

    /// <summary>
    /// The estimate behind a public ID, for the airings a list read filled in
    /// rather than read out of a provider's line. An estimate's ID derives from
    /// its schedule's and its episode's the way a stored airing's does, and a
    /// client that was handed one has no way of telling the two apart, so the
    /// same ID has to resolve either way.
    /// </summary>
    /// <remarks>
    /// The ID can't be inverted, so the schedules are walked and their episodes'
    /// IDs are derived until one matches, which is the same sweep a range read
    /// makes to work out what it can estimate at all, and no more expensive.
    /// </remarks>
    /// <param name="context">The read the view belongs to.</param>
    /// <param name="airingID">The airing's public ID.</param>
    /// <returns>The estimate, or <see langword="null"/> when no schedule makes one under that ID.</returns>
    private IEpisodeAiring? GetEstimateByID(AiringReadContext context, Guid airingID)
    {
        foreach (var row in RepoFactory.AiringSchedule.GetAll())
        {
            var covered = RepoFactory.EpisodeAiring.GetByScheduleID(row.AiringScheduleID)
                .Select(entry => (entry.EpisodeSource, entry.EpisodeID))
                .ToHashSet();
            foreach (var episode in GetScheduleEpisodes(context, row))
            {
                var key = GetEntityKey(episode);
                if (covered.Contains(key))
                    continue;
                if (AiringScheduleUtility.GetEpisodeAiringID(row.ID, AiringScheduleUtility.GetDerivedAiringKey(key.Source, key.ID)) != airingID)
                    continue;
                if (Estimate(context, row, context.GetSchedule(row), episode, key) is { } estimate)
                    return estimate;
            }
        }

        return null;
    }

    #endregion

    #region Episode Airings | Helpers

    /// <summary>
    /// One submitted airing, resolved down to the key and episode it is stored
    /// under.
    /// </summary>
    /// <param name="Key">The airing's key, submitted or derived.</param>
    /// <param name="Source">The source of the episode.</param>
    /// <param name="ID">The ID of the episode within its source.</param>
    /// <param name="Data">The submission itself.</param>
    private sealed record AiringSubmission(string Key, DataSource Source, string ID, EpisodeAiringData Data);

    /// <summary>
    /// What a write did to one airing row, which is what the three lists on
    /// <see cref="EpisodeAiringsUpdatedEventArgs"/> are filled from.
    /// </summary>
    private enum AiringWriteState
    {
        /// <summary>
        /// The write handed the row back exactly as it was stored, so it was
        /// neither rewritten nor reported.
        /// </summary>
        Unchanged = 0,

        /// <summary>
        /// The row is new to the schedule.
        /// </summary>
        Added = 1,

        /// <summary>
        /// The row was already there and something about it moved.
        /// </summary>
        Updated = 2,

        /// <summary>
        /// The write took the row off the schedule's listing and kept it
        /// without a slot, which is how a hiatus is stored.
        /// </summary>
        Withdrawn = 3,
    }

    /// <summary>
    /// Everything a write can change on an airing row, so a write that changed
    /// something can be told from one that handed the row back as it was.
    /// </summary>
    /// <param name="Key">The airing's key.</param>
    /// <param name="EpisodeSource">The source of the episode.</param>
    /// <param name="EpisodeID">The ID of the episode within its source.</param>
    /// <param name="Url">The airing's url.</param>
    /// <param name="AiredAt">The airing's slot.</param>
    /// <param name="OriginalAiredAt">The first slot the airing was scheduled for.</param>
    /// <param name="IsDelayed">Whether the airing's own slot was postponed.</param>
    /// <param name="LinkedToID">The local ID of the airing's link head.</param>
    private readonly record struct AiringRowState(
        string Key,
        DataSource EpisodeSource,
        string EpisodeID,
        string? Url,
        DateTime? AiredAt,
        DateTime? OriginalAiredAt,
        bool IsDelayed,
        int? LinkedToID
    );

    /// <summary>
    /// The state of an airing row as it stands, taken before and after a write
    /// touches it.
    /// </summary>
    /// <param name="entry">The stored airing.</param>
    /// <returns>The row's state.</returns>
    private static AiringRowState GetRowState(EpisodeAiring entry)
        => new(entry.Key, entry.EpisodeSource, entry.EpisodeID, entry.Url, entry.AiredAt, entry.OriginalAiredAt, entry.IsDelayed, entry.LinkedToID);

    /// <summary>
    /// The one coarse reason a write dispatches, for a consumer that doesn't
    /// read the three lists apart.
    /// </summary>
    /// <param name="added">How many airings the write added.</param>
    /// <param name="updated">How many airings the write changed.</param>
    /// <param name="withdrawn">How many airings the write took off the listing.</param>
    /// <returns>The reason.</returns>
    private static UpdateReason GetWriteReason(int added, int updated, int withdrawn)
        => (added, updated, withdrawn) switch
        {
            (0, 0, 0) => UpdateReason.None,
            ( > 0, 0, 0) => UpdateReason.Added,
            (0, 0, > 0) => UpdateReason.Removed,
            _ => UpdateReason.Updated,
        };

    /// <summary>
    /// The enriched views of the rows a write touched, in slot order.
    /// </summary>
    /// <param name="context">The read the views belong to.</param>
    /// <param name="scheduleView">The schedule the airings are on.</param>
    /// <param name="rows">The rows to build views over.</param>
    /// <returns>The views.</returns>
    private static List<IEpisodeAiring> ToViews(AiringReadContext context, AiringScheduleView scheduleView, IReadOnlyList<EpisodeAiring> rows)
        => rows
            .OrderBy(entry => entry.AiredAt ?? entry.OriginalAiredAt ?? DateTime.MaxValue)
            .ThenBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(IEpisodeAiring (entry) => new EpisodeAiringView(context, scheduleView, entry))
            .ToList();

    /// <summary>
    /// Check every airing of a write at once, so a batch reports all of its
    /// rejected airings rather than failing on the first.
    /// </summary>
    /// <param name="context">The read the resolutions belong to.</param>
    /// <param name="row">The schedule being written to.</param>
    /// <param name="airings">The submitted airings.</param>
    /// <param name="now">The current time, in UTC.</param>
    /// <param name="removalKeys">Optional. The keys the same write takes away, which nothing it submits may name.</param>
    /// <returns>The accepted submissions, in submission order.</returns>
    /// <exception cref="AiringScheduleValidationException">An airing is outside the schedule's series, season or coverage, shares a key with another, is also being removed, or is older than the retention window.</exception>
    private List<AiringSubmission> ValidateAirings(
        AiringReadContext context,
        AiringSchedule row,
        IEnumerable<EpisodeAiringData> airings,
        DateTime now,
        IReadOnlySet<string>? removalKeys = null
    )
    {
        var settings = LoadSettings();
        var window = settings.AutoCleanup ? now.AddMonths(-settings.RetentionMonths) : (DateTime?)null;
        var season = string.IsNullOrEmpty(row.SeasonID) ? null : context.GetSeason(row.SeriesSource, row.SeasonID);
        var seasonEpisodes = season?.Episodes.Select(GetEntityKey).ToHashSet();
        var submissions = new List<AiringSubmission>();
        var errors = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var airing in airings)
        {
            var position = index++;
            if (airing is null)
                continue;

            var problems = new List<string>();
            if (airing.Episode is not { } episode)
            {
                errors[$"#{position}"] = ["An airing needs an episode."];
                continue;
            }

            var (source, id) = GetEntityKey(episode);
            var key = string.IsNullOrWhiteSpace(airing.Key) ? AiringScheduleUtility.GetDerivedAiringKey(source, id) : airing.Key.Trim();
            if (!seen.Add(key))
                problems.Add($"Two airings share the key \"{key}\".");
            if (removalKeys is not null && removalKeys.Contains(key))
                problems.Add("The airing is both submitted and removed by the same write.");
            if (source != row.SeriesSource || episode.SeriesID.ToString() != row.SeriesID)
                problems.Add("The episode does not belong to the schedule's series.");
            else if (seasonEpisodes is not null && !seasonEpisodes.Contains((source, id)))
                problems.Add("The episode does not belong to the schedule's season.");
            if (episode.Type is EpisodeType.Episode)
            {
                if (row.FirstEpisodeNumber is { } first && episode.EpisodeNumber < first)
                    problems.Add($"The episode is before the schedule's coverage, which starts at episode {first}.");
                if (row.LastEpisodeNumber is { } last && episode.EpisodeNumber > last)
                    problems.Add($"The episode is past the schedule's coverage, which ends at episode {last}.");
            }

            if (window is { } cutoff && (airing.AiredAt ?? airing.OriginalAiredAt) is { } slot && slot < cutoff)
                problems.Add($"The airing is older than the {settings.RetentionMonths} month retention window, which the next sweep would remove it under.");

            if (problems.Count > 0)
                errors[key] = problems;
            else
                submissions.Add(new AiringSubmission(key, source, id, airing));
        }

        if (errors.Count > 0)
            throw new AiringScheduleValidationException("One or more airings were rejected.", errors);

        return submissions;
    }

    /// <summary>
    /// Refuse a single airing whose slot the retention sweep would only remove
    /// again.
    /// </summary>
    /// <param name="key">The airing's key, which the error is reported under.</param>
    /// <param name="slot">The airing's slot.</param>
    /// <param name="now">The current time, in UTC.</param>
    /// <exception cref="AiringScheduleValidationException">The airing is older than the retention window while cleanup is on.</exception>
    private void ValidateRetention(string key, DateTime? slot, DateTime now)
    {
        var settings = LoadSettings();
        if (!settings.AutoCleanup || slot is not { } value || value >= now.AddMonths(-settings.RetentionMonths))
            return;

        throw new AiringScheduleValidationException("One or more airings were rejected.", new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            [key] = [$"The airing is older than the {settings.RetentionMonths} month retention window, which the next sweep would remove it under."],
        });
    }

    /// <summary>
    /// The episode keys of this schedule's slotless airings that another
    /// channel-mate has since aired, which is what turns a kept hiatus row into
    /// history.
    /// </summary>
    /// <param name="row">The schedule being written to.</param>
    /// <param name="existingRows">The schedule's current airings.</param>
    /// <param name="now">The current time, in UTC.</param>
    /// <returns>The superseded episode keys.</returns>
    private HashSet<string> GetSupersededEpisodeKeys(AiringSchedule row, IReadOnlyList<EpisodeAiring> existingRows, DateTime now)
    {
        var superseded = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in existingRows.Where(entry => entry.AiredAt is null))
        {
            foreach (var other in RepoFactory.EpisodeAiring.GetByEpisodeID(entry.EpisodeSource, entry.EpisodeID))
            {
                if (other.EpisodeAiringID == entry.EpisodeAiringID || other.AiredAt is not { } airedAt || airedAt > now)
                    continue;
                if (RepoFactory.AiringSchedule.GetByID(other.AiringScheduleID) is not { } otherSchedule)
                    continue;
                // Another channel airing the episode doesn't supersede it, and a
                // channel-less airing is only superseded by another one.
                if (otherSchedule.ChannelID != row.ChannelID || GetProviderInfo(otherSchedule.ProviderID) is not { Enabled: true })
                    continue;
                if (!HasMatchingTrack(row.Tracks, otherSchedule.Tracks))
                    continue;

                superseded.Add(AiringScheduleUtility.GetDerivedAiringKey(entry.EpisodeSource, entry.EpisodeID));
                break;
            }
        }

        return superseded;
    }

    /// <summary>
    /// Whether two schedules release anything in common: the same kind in the
    /// same language, matched through <see cref="Shoko.Abstractions.Metadata.Enums.TitleLanguage"/>
    /// where the enum knows the codes and on the raw pair where it doesn't.
    /// </summary>
    /// <param name="left">One schedule's tracks.</param>
    /// <param name="right">The other schedule's tracks.</param>
    /// <returns><see langword="true"/> when the two share a track.</returns>
    private static bool HasMatchingTrack(IReadOnlyList<AiringTrackData> left, IReadOnlyList<AiringTrackData> right)
    {
        var ours = left.Select(track => new AiringTrack(track)).ToList();
        var theirs = right.Select(track => new AiringTrack(track)).ToList();
        return ours.Any(one => theirs.Any(other => TracksMatch(one, other)));
    }

    /// <summary>
    /// Whether two tracks are the same release: the same kind, matched through
    /// <see cref="TitleLanguage"/> where the enum knows both codes and on the
    /// raw code pair where it doesn't.
    /// </summary>
    /// <param name="one">One track.</param>
    /// <param name="other">The other track.</param>
    /// <returns><see langword="true"/> when the two are the same release.</returns>
    internal static bool TracksMatch(IAiringTrack one, IAiringTrack other)
    {
        if (one.Kind != other.Kind)
            return false;
        if (one.Language is TitleLanguage.Unknown or TitleLanguage.None || other.Language is TitleLanguage.Unknown or TitleLanguage.None)
            return string.Equals(one.LanguageCode, other.LanguageCode, StringComparison.Ordinal) &&
                string.Equals(one.CountryCode, other.CountryCode, StringComparison.Ordinal);

        return one.Language == other.Language;
    }

    /// <summary>
    /// Whether a slot moved far enough to be a move rather than a correction.
    /// </summary>
    /// <param name="from">The slot the airing had.</param>
    /// <param name="to">The slot it moved to.</param>
    /// <returns><see langword="true"/> when the first scheduled slot should be kept.</returns>
    private static bool HasMoved(DateTime? from, DateTime? to)
        => from is { } before && (to is not { } after || (after - before).Duration() >= AiringInferenceOptions.DefaultMoveThreshold);

    /// <summary>
    /// The episode number an airing counts as for coverage, which only normal
    /// episodes have; a special is never in a range and never estimated.
    /// </summary>
    /// <param name="context">The read the resolution belongs to.</param>
    /// <param name="source">The source of the episode.</param>
    /// <param name="id">The ID of the episode within its source.</param>
    /// <returns>The episode number, or <see langword="null"/>.</returns>
    private static int? GetNormalEpisodeNumber(AiringReadContext context, DataSource source, string id)
        => context.GetEpisode(source, id) is { Type: EpisodeType.Episode } episode ? episode.EpisodeNumber : null;

    /// <summary>
    /// The stored airing a provider handed back, checked against the owner
    /// stored on its schedule.
    /// </summary>
    /// <param name="info">The registered provider making the change.</param>
    /// <param name="airing">The airing the provider handed in.</param>
    /// <param name="paramName">The name of the argument the airing arrived as.</param>
    /// <returns>The airing's schedule and the stored airing.</returns>
    /// <exception cref="ArgumentException">The airing is unknown or estimated, or its schedule is owned by another provider.</exception>
    private (AiringSchedule Schedule, EpisodeAiring Airing) GetOwnedAiring(AiringScheduleProviderInfo info, IEpisodeAiring airing, string paramName)
    {
        if (airing.IsEstimated)
            throw new ArgumentException("An estimate is not a stored airing.", paramName);

        var entry = GetAiringRow(airing.ID)
            ?? throw new ArgumentException($"Unknown airing: '{airing.ID}'", paramName);
        var row = RepoFactory.AiringSchedule.GetByID(entry.AiringScheduleID)
            ?? throw new ArgumentException($"Unknown airing: '{airing.ID}'", paramName);
        if (row.ProviderID != info.ID)
            throw new ArgumentException("The airing is owned by another provider.", paramName);

        return (row, entry);
    }

    /// <summary>
    /// The keys of the airings a delta write names for removal, checked the same
    /// way every other change is: the provider has to own them, and they have to
    /// be on the schedule being written to.
    /// </summary>
    /// <param name="info">The registered provider making the change.</param>
    /// <param name="row">The schedule being written to.</param>
    /// <param name="removals">The airings the provider handed in, which may be <see langword="null"/> or empty.</param>
    /// <returns>The keys to remove, which is empty when nothing was named.</returns>
    /// <exception cref="ArgumentException">An airing is estimated or unknown, is owned by another provider, or is on another schedule.</exception>
    private IReadOnlySet<string> ResolveRemovals(AiringScheduleProviderInfo info, AiringSchedule row, IEnumerable<IEpisodeAiring>? removals)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        if (removals is null)
            return keys;

        foreach (var airing in removals)
        {
            if (airing is null)
                continue;

            var (schedule, entry) = GetOwnedAiring(info, airing, nameof(removals));
            if (schedule.AiringScheduleID != row.AiringScheduleID)
                throw new ArgumentException("Every airing removed by a write must be on the schedule it writes to.", nameof(removals));

            keys.Add(entry.Key);
        }

        return keys;
    }

    /// <summary>
    /// The stored airing behind a public ID.
    /// </summary>
    /// <param name="airingID">The airing's public ID.</param>
    /// <returns>The stored airing, or <see langword="null"/> when it is unknown.</returns>
    private EpisodeAiring? GetAiringRow(Guid airingID)
        => GetAiringIDs().TryGetValue(airingID, out var id) ? RepoFactory.EpisodeAiring.GetByID(id) : null;

    /// <summary>
    /// The map from an airing's public ID to its local one. An airing's public
    /// ID derives from its schedule's, so it can't be an index on the airing
    /// cache; the service owns every write, so it keeps the map instead. Writes
    /// arrive on queue-worker threads while reads arrive on request threads, so
    /// the map itself is concurrent; only the one-off build is locked.
    /// </summary>
    /// <returns>The map.</returns>
    private ConcurrentDictionary<Guid, int> GetAiringIDs()
    {
        if (_airingIDs is { } known)
            return known;

        lock (_lock)
        {
            if (_airingIDs is { } raced)
                return raced;

            var map = new ConcurrentDictionary<Guid, int>();
            foreach (var entry in RepoFactory.EpisodeAiring.GetAll())
                if (RepoFactory.AiringSchedule.GetByID(entry.AiringScheduleID) is { } row)
                    map[AiringScheduleUtility.GetEpisodeAiringID(row.ID, entry.Key)] = entry.EpisodeAiringID;

            return _airingIDs = map;
        }
    }

    /// <summary>
    /// Record an airing's public ID, so a write doesn't cost a rebuild of the
    /// map.
    /// </summary>
    /// <param name="row">The airing's schedule.</param>
    /// <param name="entry">The stored airing.</param>
    private void RememberAiringID(AiringSchedule row, EpisodeAiring entry)
    {
        if (_airingIDs is { } map)
            map[AiringScheduleUtility.GetEpisodeAiringID(row.ID, entry.Key)] = entry.EpisodeAiringID;
    }

    /// <summary>
    /// Drop an airing's public ID, before it is deleted or re-keyed.
    /// </summary>
    /// <param name="row">The airing's schedule.</param>
    /// <param name="entry">The stored airing.</param>
    private void ForgetAiringID(AiringSchedule row, EpisodeAiring entry)
        => _airingIDs?.TryRemove(AiringScheduleUtility.GetEpisodeAiringID(row.ID, entry.Key), out _);

    /// <summary>
    /// Drop an airing's public ID when only the airing is in hand.
    /// </summary>
    /// <param name="entry">The stored airing.</param>
    private void ForgetAiringID(EpisodeAiring entry)
    {
        if (RepoFactory.AiringSchedule.GetByID(entry.AiringScheduleID) is { } row)
            ForgetAiringID(row, entry);
    }

    /// <summary>
    /// The service's settings, with the retention window clamped to what it
    /// will actually honour.
    /// </summary>
    /// <returns>The settings.</returns>
    internal AiringScheduleServiceSettings LoadSettings()
    {
        var settings = configurationProvider.Load();
        if (settings.RetentionMonths < AiringScheduleServiceSettings.MinimumRetentionMonths)
            settings.RetentionMonths = AiringScheduleServiceSettings.MinimumRetentionMonths;

        return settings;
    }

    #endregion
}
