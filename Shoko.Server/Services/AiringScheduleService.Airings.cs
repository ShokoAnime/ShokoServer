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
        options ??= new EpisodeAiringUpdateOptions();

        var now = DateTime.UtcNow;
        var context = new AiringReadContext(this, includeDisabled: true);
        var scheduleView = context.GetSchedule(row);
        var submissions = ValidateAirings(context, row, airings, now);
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
        var result = AiringScheduleUtility.InferAirings(existing, submitted, now, new AiringInferenceOptions
        {
            InferDelays = options.InferDelays,
            IsFinished = row.IsFinished,
            FirstEpisodeNumber = row.FirstEpisodeNumber,
            LastEpisodeNumber = row.LastEpisodeNumber,
            SupersededEpisodeKeys = GetSupersededEpisodeKeys(row, existingRows, now),
        });

        // Removals go first, so a re-keyed airing can't collide with a row that
        // is on its way out, and a promoted link head is picked from what lives.
        foreach (var airing in result.ToDelete)
        {
            if (!existingByKey.TryGetValue(airing.Key, out var entry))
                continue;

            ForgetAiringID(row, entry);
            existingByKey.Remove(airing.Key);
            RepoFactory.EpisodeAiring.Delete(entry);
        }

        var submissionsByKey = submissions.ToDictionary(entry => entry.Key, StringComparer.Ordinal);
        var renamedKeys = new Dictionary<string, string>(StringComparer.Ordinal);
        var saved = new List<EpisodeAiring>();
        foreach (var airing in result.ToSave)
        {
            var entry = airing.ExistingKey is { } existingKey && existingByKey.TryGetValue(existingKey, out var match)
                ? match
                : new EpisodeAiring() { AiringScheduleID = row.AiringScheduleID, CreatedAt = now };
            if (entry.EpisodeAiringID is not 0 && !string.Equals(entry.Key, airing.Key, StringComparison.Ordinal))
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
            entry.LastUpdatedAt = now;
            RepoFactory.EpisodeAiring.Save(entry);
            RememberAiringID(row, entry);
            saved.Add(entry);
        }

        // Links are carried as keys through the inference, so they are re-pointed
        // once every row has its final key and local ID.
        var savedByKey = saved.ToDictionary(entry => entry.Key, StringComparer.Ordinal);
        foreach (var (airing, entry) in result.ToSave.Zip(saved))
        {
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
            RepoFactory.EpisodeAiring.Save(entry);
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

        var views = saved
            .OrderBy(entry => entry.AiredAt ?? entry.OriginalAiredAt ?? DateTime.MaxValue)
            .ThenBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(IEpisodeAiring (entry) => new EpisodeAiringView(context, scheduleView, entry))
            .ToList();
        AiringsUpdated?.Invoke(this, new EpisodeAiringsUpdatedEventArgs { Reason = UpdateReason.Updated, Schedule = scheduleView, Airings = views });
        return views;
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

        var view = new EpisodeAiringView(context, scheduleView, entry);
        AiringsUpdated?.Invoke(this, new EpisodeAiringsUpdatedEventArgs { Reason = reason, Schedule = scheduleView, Airings = [view] });
        return view;
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
        AiringsUpdated?.Invoke(this, new EpisodeAiringsUpdatedEventArgs { Reason = UpdateReason.Updated, Schedule = scheduleView, Airings = [view] });
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

        AiringsUpdated?.Invoke(this, new EpisodeAiringsUpdatedEventArgs { Reason = UpdateReason.Removed, Schedule = scheduleView, Airings = [view] });
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
        AiringsUpdated?.Invoke(this, new EpisodeAiringsUpdatedEventArgs { Reason = UpdateReason.Updated, Schedule = scheduleView, Airings = views });
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
        AiringsUpdated?.Invoke(this, new EpisodeAiringsUpdatedEventArgs { Reason = UpdateReason.Updated, Schedule = scheduleView, Airings = [view] });
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

        if (GetAiringRow(airingID) is not { } entry)
            return null;

        var context = new AiringReadContext(this, includeDisabled: true);
        return context.GetSchedule(entry.AiringScheduleID) is { } scheduleView
            ? new EpisodeAiringView(context, scheduleView, entry)
            : null;
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
    /// Check every airing of a write at once, so a batch reports all of its
    /// rejected airings rather than failing on the first.
    /// </summary>
    /// <param name="context">The read the resolutions belong to.</param>
    /// <param name="row">The schedule being written to.</param>
    /// <param name="airings">The submitted airings.</param>
    /// <param name="now">The current time, in UTC.</param>
    /// <returns>The accepted submissions, in submission order.</returns>
    /// <exception cref="AiringScheduleValidationException">An airing is outside the schedule's series, season or coverage, shares a key with another, or is older than the retention window.</exception>
    private List<AiringSubmission> ValidateAirings(AiringReadContext context, AiringSchedule row, IEnumerable<EpisodeAiringData> airings, DateTime now)
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
