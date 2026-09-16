using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Utilities.Airing;

namespace Shoko.Server.Utilities;

/// <summary>
/// The pure logic behind airing schedules: the keys a schedule and an airing
/// derive when a provider gives none, the delay inference a write runs, the
/// profile a schedule's estimates come from, and the cadence a line of releases
/// keeps. Nothing here touches a repository, a service or an entity, so every
/// part of it can be exercised on its own.
/// </summary>
public static class AiringScheduleUtility
{
    #region Keys

    /// <summary>
    /// Derive the key of a schedule that the provider gave none for, from its
    /// channel and its full track set. Adding a language to a keyless schedule
    /// therefore makes a new schedule, which is why providers should pass a key
    /// of their own.
    /// </summary>
    /// <param name="channelID">The schedule's channel, or <see langword="null"/> when it has none.</param>
    /// <param name="tracks">The schedule's tracks. Duplicates collapse.</param>
    /// <returns>The derived key.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tracks"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="tracks"/> is empty.</exception>
    public static string GetDerivedScheduleKey(Guid? channelID, IEnumerable<AiringTrackData> tracks)
    {
        ArgumentNullException.ThrowIfNull(tracks);

        var trackKeys = tracks
            .Where(track => track is not null)
            .Select(GetTrackKey)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();
        if (trackKeys.Count is 0)
            throw new ArgumentException("A schedule needs at least one track to derive a key from.", nameof(tracks));

        return $"{(channelID.HasValue ? channelID.Value.ToString("D") : "-")}:{string.Join(",", trackKeys)}";
    }

    /// <summary>
    /// Derive the key of an airing that the provider gave none for, from the
    /// episode it is for.
    /// </summary>
    /// <param name="episodeSource">The source of the episode.</param>
    /// <param name="episodeID">The ID of the episode within its source.</param>
    /// <returns>The derived key.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="episodeID"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="episodeID"/> is blank.</exception>
    public static string GetDerivedAiringKey(DataSource episodeSource, string episodeID)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(episodeID);

        return $"{episodeSource}:{episodeID}";
    }

    /// <summary>
    /// Derive the key of an airing for a provider whose own ID names a whole slot
    /// rather than a single episode, by appending the episode's place in the slot.
    /// </summary>
    /// <param name="slotKey">The provider's key for the slot.</param>
    /// <param name="episodeIndex">The episode's zero-based place in the slot.</param>
    /// <returns>The derived key.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="slotKey"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="slotKey"/> is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="episodeIndex"/> is negative.</exception>
    public static string GetDerivedSlotAiringKey(string slotKey, int episodeIndex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slotKey);
        ArgumentOutOfRangeException.ThrowIfNegative(episodeIndex);

        return $"{slotKey}:{episodeIndex}";
    }

    /// <summary>
    /// Key one track, so a derived schedule key is stable whatever order or
    /// casing the provider submitted its tracks in.
    /// </summary>
    /// <param name="track">The track to key.</param>
    /// <returns>The track's key.</returns>
    private static string GetTrackKey(AiringTrackData track)
    {
        var languageCode = string.IsNullOrWhiteSpace(track.LanguageCode) ? "unk" : track.LanguageCode.Trim().ToLowerInvariant();
        var countryCode = string.IsNullOrWhiteSpace(track.CountryCode) ? null : track.CountryCode.Trim().ToUpperInvariant();
        return countryCode is null ? $"{track.Kind}/{languageCode}" : $"{track.Kind}/{languageCode}/{countryCode}";
    }

    #endregion

    #region Offsets

    /// <summary>
    /// Default number of most recent samples to learn from, so a long-running
    /// series follows its current time slot rather than one it had years ago.
    /// </summary>
    public const int DefaultWindow = 10;

    /// <summary>
    /// Learn the offset between an AniDB air date, taken as midnight UTC, and
    /// the actual air time in UTC, from the most recent samples.
    /// </summary>
    /// <param name="samples">Pairs of the AniDB air date and the precise air time (UTC) for the same episode.</param>
    /// <param name="window">How many of the most recent samples to consider.</param>
    /// <param name="minimumSamples">How many samples are needed before an offset is trusted.</param>
    /// <returns>The median offset, or <see langword="null"/> with too few samples.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="samples"/> is <see langword="null"/>.</exception>
    public static TimeSpan? LearnAirTimeOffset(IEnumerable<(DateTime AnidbAirDate, DateTime AiredAtUtc)> samples, int window = DefaultWindow, int minimumSamples = 2)
    {
        ArgumentNullException.ThrowIfNull(samples);

        return LearnOffset(samples.Select(sample => (GetMidnightUtc(sample.AnidbAirDate), sample.AiredAtUtc)), window, minimumSamples);
    }

    /// <summary>
    /// Learn the offset between an anchor instant and the actual air time in UTC,
    /// from the most recent samples. The anchor is midnight UTC of the AniDB air
    /// date for most schedules, and the episode's earliest known Original airing
    /// for a subtitled or dubbed one.
    /// </summary>
    /// <param name="samples">Pairs of the anchor (UTC) and the precise air time (UTC) for the same episode.</param>
    /// <param name="window">How many of the most recent samples to consider.</param>
    /// <param name="minimumSamples">How many samples are needed before an offset is trusted.</param>
    /// <returns>The median offset, or <see langword="null"/> with too few samples.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="samples"/> is <see langword="null"/>.</exception>
    public static TimeSpan? LearnOffset(IEnumerable<(DateTime AnchorUtc, DateTime AiredAtUtc)> samples, int window = DefaultWindow, int minimumSamples = 2)
    {
        ArgumentNullException.ThrowIfNull(samples);

        var offsets = samples
            .OrderByDescending(sample => sample.AnchorUtc)
            .Take(window)
            .Select(sample => sample.AiredAtUtc - sample.AnchorUtc)
            .ToList();
        if (offsets.Count < minimumSamples)
            return null;

        // The median, so a special aired in another slot or a one-off delay doesn't skew it.
        return GetMedian(offsets);
    }

    /// <summary>
    /// Apply a learned offset to an AniDB air date.
    /// </summary>
    /// <param name="anidbAirDate">The AniDB air date.</param>
    /// <param name="offset">The learned offset from midnight UTC.</param>
    /// <returns>The estimated air time in UTC.</returns>
    public static DateTime EstimateAirTime(DateTime anidbAirDate, TimeSpan offset)
        => GetMidnightUtc(anidbAirDate) + offset;

    /// <summary>
    /// Midnight UTC of a date, whatever kind the value carried.
    /// </summary>
    /// <param name="date">The date to take midnight of.</param>
    /// <returns>Midnight UTC of the date.</returns>
    private static DateTime GetMidnightUtc(DateTime date)
        => DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);

    /// <summary>
    /// The median of a set of spans, averaging the two middle values for an even
    /// count.
    /// </summary>
    /// <param name="values">The values to take the median of. At least one is required.</param>
    /// <returns>The median value.</returns>
    private static TimeSpan GetMedian(IReadOnlyCollection<TimeSpan> values)
    {
        var ordered = values.OrderBy(value => value).ToList();
        var middle = ordered.Count / 2;
        return ordered.Count % 2 is 1
            ? ordered[middle]
            : TimeSpan.FromTicks((ordered[middle - 1].Ticks + ordered[middle].Ticks) / 2);
    }

    #endregion

    #region Delay inference

    /// <summary>
    /// Work out what a write does to one schedule's line of airings: which rows
    /// to store, with the delay state they end up in, and which rows to remove.
    /// The line is followed in the order the existing airings were in, so a
    /// pre-emption on one station, or a dub slipping while the subtitled release
    /// doesn't, stays on its own schedule.
    /// </summary>
    /// <param name="existingAirings">The schedule's current airings.</param>
    /// <param name="submittedAirings">The airings the provider submitted, replacing the current ones.</param>
    /// <param name="now">The current time, in UTC. It decides whether a removed airing is a hiatus or history, and when a slotless airing has expired.</param>
    /// <param name="options">Optional. The schedule's coverage and the thresholds to infer with. Defaults to the service's own.</param>
    /// <returns>The airings to store and the airings to remove.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="existingAirings"/> or <paramref name="submittedAirings"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Two existing or two submitted airings share a key.</exception>
    public static AiringInferenceResult InferAirings(
        IEnumerable<ExistingAiring> existingAirings,
        IEnumerable<SubmittedAiring> submittedAirings,
        DateTime now,
        AiringInferenceOptions? options = null
    )
    {
        ArgumentNullException.ThrowIfNull(existingAirings);
        ArgumentNullException.ThrowIfNull(submittedAirings);
        options ??= new AiringInferenceOptions();

        var existing = existingAirings.Where(airing => airing is not null).ToList();
        var submitted = submittedAirings.Where(airing => airing is not null).ToList();
        var existingByKey = new Dictionary<string, ExistingAiring>(StringComparer.Ordinal);
        foreach (var airing in existing)
            if (!existingByKey.TryAdd(airing.Key, airing))
                throw new ArgumentException($"Two existing airings share the key \"{airing.Key}\".", nameof(existingAirings));

        var submittedKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var airing in submitted)
            if (!submittedKeys.Add(airing.Key))
                throw new ArgumentException($"Two submitted airings share the key \"{airing.Key}\".", nameof(submittedAirings));

        var pairs = PairAirings(existing, submitted, existingByKey);
        var pairedKeys = pairs.Values
            .Where(airing => airing is not null)
            .Select(airing => airing!.Key)
            .ToHashSet(StringComparer.Ordinal);
        var removed = existing.Where(airing => !pairedKeys.Contains(airing.Key)).ToList();
        if (!options.InferDelays)
            return StoreAsSubmitted(existing, submitted, pairs, removed);

        var pending = new List<PendingAiring>();
        foreach (var airing in submitted)
            pending.Add(CreatePending(airing, pairs[airing.Key], options));

        var toDelete = new List<ExistingAiring>();
        foreach (var airing in removed)
        {
            // An explicit removal on a finished schedule, or outside what the
            // schedule covers, is history rather than a hiatus.
            if (options.IsFinished || !IsWithinCoverage(airing.EpisodeNumber, options))
            {
                toDelete.Add(airing);
                continue;
            }

            // A slotless airing lives until something contradicts it.
            if (airing.AiredAt is null)
            {
                if (options.SupersededEpisodeKeys is { } superseded && superseded.Contains(airing.EpisodeKey))
                    toDelete.Add(airing);
                else if (airing.OriginalAiredAt is { } originalAiredAt && originalAiredAt + options.SlotlessRetention <= now)
                    toDelete.Add(airing);
                continue;
            }

            // A removed airing whose slot is still ahead of us is how a source
            // indicates a hiatus; one in the past is simply gone.
            if (airing.AiredAt > now)
                pending.Add(CreateHiatusPending(airing));
            else
                toDelete.Add(airing);
        }

        DetectCauses(pending, options);

        var toSave = pending
            .Select(entry => new InferredAiring
            {
                Key = entry.Key,
                EpisodeKey = entry.EpisodeKey,
                ExistingKey = entry.Existing?.Key,
                AiredAt = entry.AiredAt,
                OriginalAiredAt = entry.OriginalAiredAt,
                IsDelayed = entry.IsDelayed,
                LinkKey = entry.LinkKey,
            })
            .ToList();
        UnlinkStaleMembers(toSave, existing, toDelete);
        return new AiringInferenceResult(toSave, toDelete);
    }

    /// <summary>
    /// Match every submitted airing to the existing airing it replaces: by key
    /// first, then, for what is left, by episode, so an entry a source recreated
    /// under a new key keeps its row instead of leaving a stale one behind.
    /// </summary>
    /// <param name="existing">The schedule's current airings.</param>
    /// <param name="submitted">The airings the provider submitted.</param>
    /// <param name="existingByKey">The current airings by key.</param>
    /// <returns>The existing airing for every submitted key, or <see langword="null"/> where there is none.</returns>
    private static Dictionary<string, ExistingAiring?> PairAirings(
        IReadOnlyList<ExistingAiring> existing,
        IReadOnlyList<SubmittedAiring> submitted,
        Dictionary<string, ExistingAiring> existingByKey
    )
    {
        var pairs = new Dictionary<string, ExistingAiring?>(StringComparer.Ordinal);
        var paired = new HashSet<string>(StringComparer.Ordinal);
        foreach (var airing in submitted)
        {
            if (existingByKey.TryGetValue(airing.Key, out var match))
            {
                pairs[airing.Key] = match;
                paired.Add(match.Key);
                continue;
            }

            pairs[airing.Key] = null;
        }

        var unpaired = existing.Where(airing => !paired.Contains(airing.Key)).ToList();
        foreach (var airing in submitted.Where(entry => pairs[entry.Key] is null))
        {
            var match = unpaired
                .Where(entry => string.Equals(entry.EpisodeKey, airing.EpisodeKey, StringComparison.Ordinal))
                // A slotless airing first, so a recreated entry claims the hiatus row it left behind.
                .OrderBy(entry => entry.AiredAt.HasValue ? 1 : 0)
                .ThenBy(entry => entry.AiredAt ?? entry.OriginalAiredAt ?? DateTime.MaxValue)
                .ThenBy(entry => entry.Key, StringComparer.Ordinal)
                .FirstOrDefault();
            if (match is null)
                continue;

            pairs[airing.Key] = match;
            unpaired.Remove(match);
        }

        return pairs;
    }

    /// <summary>
    /// Store the submitted airings as they came in, and delete everything that
    /// went, for a provider that reports delays itself.
    /// </summary>
    /// <param name="existing">The schedule's current airings.</param>
    /// <param name="submitted">The airings the provider submitted.</param>
    /// <param name="pairs">The existing airing for every submitted key.</param>
    /// <param name="removed">The existing airings the provider no longer reports.</param>
    /// <returns>The airings to store and the airings to remove.</returns>
    private static AiringInferenceResult StoreAsSubmitted(
        IReadOnlyList<ExistingAiring> existing,
        IReadOnlyList<SubmittedAiring> submitted,
        Dictionary<string, ExistingAiring?> pairs,
        IReadOnlyList<ExistingAiring> removed
    )
    {
        var toSave = submitted
            .Select(airing => new InferredAiring
            {
                Key = airing.Key,
                EpisodeKey = airing.EpisodeKey,
                ExistingKey = pairs[airing.Key]?.Key,
                AiredAt = airing.AiredAt,
                OriginalAiredAt = airing.OriginalAiredAt,
                IsDelayed = airing.IsDelayed ?? false,
                LinkKey = pairs[airing.Key]?.LinkKey,
            })
            .ToList();
        var toDelete = removed.ToList();
        UnlinkStaleMembers(toSave, existing, toDelete);
        return new AiringInferenceResult(toSave, toDelete);
    }

    /// <summary>
    /// Resolve one submitted airing against the row it replaces, leaving the
    /// cause of a move to be settled once the whole line is known.
    /// </summary>
    /// <param name="submitted">The submitted airing.</param>
    /// <param name="existing">The existing airing it replaces, or <see langword="null"/> when it is new.</param>
    /// <param name="options">The thresholds to infer with.</param>
    /// <returns>The airing's state so far.</returns>
    private static PendingAiring CreatePending(SubmittedAiring submitted, ExistingAiring? existing, AiringInferenceOptions options)
    {
        var pending = new PendingAiring
        {
            Key = submitted.Key,
            EpisodeKey = submitted.EpisodeKey,
            Existing = existing,
            ExplicitOriginalAiredAt = submitted.OriginalAiredAt,
            ExplicitIsDelayed = submitted.IsDelayed,
            AiredAt = submitted.AiredAt,
            LinkKey = existing?.LinkKey,
        };
        if (existing is null)
        {
            pending.Move = AiringMove.Added;
            pending.OriginalAiredAt = submitted.OriginalAiredAt;
            pending.IsDelayed = submitted.IsDelayed ?? false;
            return pending;
        }

        pending.OriginalAiredAt = existing.OriginalAiredAt;
        pending.IsDelayed = existing.IsDelayed;
        switch (existing.AiredAt, submitted.AiredAt)
        {
            // It never had a slot, or it returned to one: either way nothing moved.
            case (null, _):
                pending.Move = AiringMove.None;
                break;

            // It lost the slot it had.
            case ({ } previous, null):
                pending.Move = AiringMove.LostSlot;
                pending.OriginalAiredAt = existing.OriginalAiredAt ?? previous;
                break;

            case ({ } previous, { } current):
                var shift = current - previous;
                if (shift.Duration() < options.MoveThreshold)
                {
                    // A correction of the current slot, not a delay.
                    pending.Move = shift == TimeSpan.Zero ? AiringMove.None : AiringMove.Correction;
                    break;
                }

                pending.Shift = shift;
                pending.Move = shift > TimeSpan.Zero ? AiringMove.Postponed : AiringMove.MovedEarlier;
                pending.OriginalAiredAt = existing.OriginalAiredAt ?? previous;
                if (pending.Move is AiringMove.MovedEarlier)
                    pending.IsDelayed = false;
                break;
        }

        return pending;
    }

    /// <summary>
    /// Keep a removed airing whose slot is still ahead of us, with no slot and
    /// the slot it lost, which is how a hiatus is stored.
    /// </summary>
    /// <param name="existing">The removed airing.</param>
    /// <returns>The airing's state so far.</returns>
    private static PendingAiring CreateHiatusPending(ExistingAiring existing)
        => new()
        {
            Key = existing.Key,
            EpisodeKey = existing.EpisodeKey,
            Existing = existing,
            AiredAt = null,
            OriginalAiredAt = existing.OriginalAiredAt ?? existing.AiredAt,
            IsDelayed = existing.IsDelayed,
            LinkKey = existing.LinkKey,
            Move = AiringMove.LostSlot,
        };

    /// <summary>
    /// Flag the airing that caused each break, and only that one. Consecutive
    /// releases that moved by the same amount, or that all lost their slot, are
    /// one break: the first of the run is delayed and the rest merely shifted. A
    /// link set counts as one release, so every member of a delayed slot is
    /// flagged together.
    /// </summary>
    /// <param name="pending">Every airing on the line, in any order.</param>
    /// <param name="options">The thresholds to infer with.</param>
    private static void DetectCauses(IReadOnlyList<PendingAiring> pending, AiringInferenceOptions options)
    {
        var units = new List<List<PendingAiring>>();
        var unitsByKey = new Dictionary<string, List<PendingAiring>>(StringComparer.Ordinal);
        var line = pending
            .Where(entry => entry.Existing is not null)
            .OrderBy(entry => entry.PreviousAiredAt ?? DateTime.MaxValue)
            .ThenBy(entry => entry.Key, StringComparer.Ordinal);
        foreach (var entry in line)
        {
            var key = entry.Existing!.LinkKey ?? $" {entry.Existing.Key}";
            if (!unitsByKey.TryGetValue(key, out var unit))
            {
                unitsByKey[key] = unit = [];
                units.Add(unit);
            }

            unit.Add(entry);
        }

        // A link set only counts as one release while its members moved
        // together; one that drifted is on its own, and is unlinked below.
        var releases = new List<List<PendingAiring>>();
        foreach (var unit in units)
        {
            if (unit.Count is 1 || unit.All(entry => entry.Move == unit[0].Move && (entry.Shift - unit[0].Shift).Duration() <= options.CauseTolerance))
                releases.Add(unit);
            else
                releases.AddRange(unit.Select(entry => new List<PendingAiring> { entry }));
        }

        var runMove = AiringMove.None;
        var runShift = TimeSpan.Zero;
        foreach (var unit in releases)
        {
            var move = unit[0].Move;
            var shift = unit[0].Shift;
            if (move is not (AiringMove.LostSlot or AiringMove.Postponed))
            {
                runMove = AiringMove.None;
                continue;
            }

            var continues = runMove == move && (move is AiringMove.LostSlot || (shift - runShift).Duration() <= options.CauseTolerance);
            foreach (var entry in unit)
                entry.IsDelayed = !continues;

            if (continues)
                continue;

            runMove = move;
            runShift = shift;
        }

        // Whatever the provider said about an airing outranks what we inferred.
        foreach (var entry in pending)
        {
            if (entry.ExplicitOriginalAiredAt is not null)
                entry.OriginalAiredAt = entry.ExplicitOriginalAiredAt;
            if (entry.ExplicitIsDelayed is { } isDelayed)
                entry.IsDelayed = isDelayed;
        }
    }

    /// <summary>
    /// Unlink every member that drifted from its link head, so a slot that no
    /// longer covers the same episodes stops being one airing.
    /// </summary>
    /// <param name="toSave">The airings about to be stored. Members that drifted are updated in place.</param>
    /// <param name="existing">The schedule's current airings.</param>
    /// <param name="toDelete">The airings about to be removed.</param>
    private static void UnlinkStaleMembers(List<InferredAiring> toSave, IReadOnlyList<ExistingAiring> existing, IReadOnlyList<ExistingAiring> toDelete)
    {
        var savedByExistingKey = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < toSave.Count; index++)
            if (toSave[index].ExistingKey is { } existingKey)
                savedByExistingKey[existingKey] = index;
        var deletedKeys = toDelete.Select(airing => airing.Key).ToHashSet(StringComparer.Ordinal);

        // What every existing row ends up as, so an untouched member can still be
        // measured against a head that moved.
        var states = new Dictionary<string, (ExistingAiring Existing, DateTime? AiredAt)>(StringComparer.Ordinal);
        foreach (var airing in existing.Where(airing => !deletedKeys.Contains(airing.Key)))
            states[airing.Key] = (airing, savedByExistingKey.TryGetValue(airing.Key, out var saved) ? toSave[saved].AiredAt : airing.AiredAt);

        foreach (var (key, (airing, airedAt)) in states)
        {
            if (airing.LinkKey is not { } linkKey || string.Equals(linkKey, key, StringComparison.Ordinal))
                continue;
            if (!states.TryGetValue(linkKey, out var head))
                continue;
            if (!HasDriftedFromHead(airing, airedAt, head.Existing, head.AiredAt))
                continue;

            if (savedByExistingKey.TryGetValue(key, out var index))
                toSave[index] = toSave[index] with { LinkKey = null };
            else
                toSave.Add(new InferredAiring
                {
                    Key = airing.Key,
                    EpisodeKey = airing.EpisodeKey,
                    ExistingKey = airing.Key,
                    AiredAt = airing.AiredAt,
                    OriginalAiredAt = airing.OriginalAiredAt,
                    IsDelayed = airing.IsDelayed,
                    LinkKey = null,
                });
        }
    }

    /// <summary>
    /// Whether a linked airing moved a day or more relative to its link head.
    /// Members may sit at different times, so what is measured is the distance
    /// between them rather than the times themselves.
    /// </summary>
    /// <param name="member">The member's existing state.</param>
    /// <param name="memberAiredAt">The member's slot after the write.</param>
    /// <param name="head">The head's existing state.</param>
    /// <param name="headAiredAt">The head's slot after the write.</param>
    /// <returns><see langword="true"/> when the member should be unlinked.</returns>
    private static bool HasDriftedFromHead(ExistingAiring member, DateTime? memberAiredAt, ExistingAiring head, DateTime? headAiredAt)
    {
        // One of them lost its slot while the other kept one.
        if (memberAiredAt.HasValue != headAiredAt.HasValue)
            return true;
        if (memberAiredAt is not { } memberSlot || headAiredAt is not { } headSlot)
            return false;
        if (member.AiredAt is not { } previousMemberSlot || head.AiredAt is not { } previousHeadSlot)
            return false;

        return ((memberSlot - headSlot) - (previousMemberSlot - previousHeadSlot)).Duration() >= AiringInferenceOptions.DefaultMoveThreshold;
    }

    /// <summary>
    /// Whether an episode number falls inside what a schedule covers. An unknown
    /// number is always inside it.
    /// </summary>
    /// <param name="episodeNumber">The episode's number, if known.</param>
    /// <param name="options">The schedule's coverage.</param>
    /// <returns><see langword="true"/> when the episode is covered.</returns>
    private static bool IsWithinCoverage(int? episodeNumber, AiringInferenceOptions options)
    {
        if (episodeNumber is not { } number)
            return true;
        if (options.FirstEpisodeNumber is { } first && number < first)
            return false;
        if (options.LastEpisodeNumber is { } last && number > last)
            return false;

        return true;
    }

    /// <summary>
    /// How an airing's slot changed in a write.
    /// </summary>
    private enum AiringMove
    {
        /// <summary>
        /// The slot didn't change.
        /// </summary>
        None = 0,

        /// <summary>
        /// A new airing, with nothing to compare against.
        /// </summary>
        Added = 1,

        /// <summary>
        /// The slot moved by less than a day, which is a correction.
        /// </summary>
        Correction = 2,

        /// <summary>
        /// The slot moved a day or more later.
        /// </summary>
        Postponed = 3,

        /// <summary>
        /// The slot moved a day or more earlier, which is never a delay.
        /// </summary>
        MovedEarlier = 4,

        /// <summary>
        /// The airing lost its slot, either because it was submitted without one
        /// or because the provider stopped reporting it.
        /// </summary>
        LostSlot = 5,
    }

    /// <summary>
    /// One airing as the inference works it out, before it becomes an
    /// <see cref="InferredAiring"/>.
    /// </summary>
    private sealed record PendingAiring
    {
        /// <summary>
        /// The airing's key after the write.
        /// </summary>
        public required string Key { get; init; }

        /// <summary>
        /// The key of the episode the airing is for.
        /// </summary>
        public required string EpisodeKey { get; init; }

        /// <summary>
        /// The row the airing updates, or <see langword="null"/> when it is new.
        /// </summary>
        public ExistingAiring? Existing { get; init; }

        /// <summary>
        /// The slot the row had before the write.
        /// </summary>
        public DateTime? PreviousAiredAt => Existing?.AiredAt ?? Existing?.OriginalAiredAt;

        /// <summary>
        /// The slot the airing ends up with.
        /// </summary>
        public DateTime? AiredAt { get; set; }

        /// <summary>
        /// The first slot the airing was scheduled for.
        /// </summary>
        public DateTime? OriginalAiredAt { get; set; }

        /// <summary>
        /// Whether the airing ends up flagged as delayed.
        /// </summary>
        public bool IsDelayed { get; set; }

        /// <summary>
        /// The link head the airing belongs to.
        /// </summary>
        public string? LinkKey { get; set; }

        /// <summary>
        /// How the slot changed.
        /// </summary>
        public AiringMove Move { get; set; }

        /// <summary>
        /// How far the slot moved, for a move of a day or more.
        /// </summary>
        public TimeSpan Shift { get; set; }

        /// <summary>
        /// The original slot the provider set itself, which always wins.
        /// </summary>
        public DateTime? ExplicitOriginalAiredAt { get; init; }

        /// <summary>
        /// The delay flag the provider set itself, which always wins.
        /// </summary>
        public bool? ExplicitIsDelayed { get; init; }
    }

    #endregion

    #region Estimates

    /// <summary>
    /// Learn what one schedule's own airings say about its slot. The profile
    /// belongs to the schedule, so two channels carrying the same series each
    /// learn their own, and one station's hiatus leaves the others alone.
    /// </summary>
    /// <param name="samples">The schedule's own stored airings. Estimates are never samples.</param>
    /// <param name="options">Optional. What the schedule's tracks and coverage say. Defaults to an open-ended Original schedule.</param>
    /// <returns>The schedule's profile. Its offset is <see langword="null"/> when too few samples were known to trust one.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="samples"/> is <see langword="null"/>.</exception>
    public static AiringScheduleProfile LearnProfile(IEnumerable<AiringProfileSample> samples, AiringProfileOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(samples);
        options ??= new AiringProfileOptions();

        var all = samples.Where(sample => sample is not null).ToList();
        var lastEstimableEpisode = options.IsFinished ? 0 : options.LastEpisodeNumber;
        var hiatusFrom = all
            .Where(sample => sample.AiredAt is null && sample.IsDelayed && sample.OriginalAiredAt.HasValue)
            .Select(sample => sample.OriginalAiredAt!.Value)
            .OrderBy(slot => slot)
            .Cast<DateTime?>()
            .FirstOrDefault();

        // A link set is one release, timed at its earliest member, in every step
        // and in every minimum below.
        var releases = CollapseLinkSets(all)
            .Where(sample => sample.AiredAt.HasValue && !sample.IsDelayed)
            .ToList();
        var anidbPairs = releases
            .Where(sample => sample.AnidbAirDate.HasValue)
            .Select(sample => (AnchorUtc: GetMidnightUtc(sample.AnidbAirDate!.Value), AiredAtUtc: sample.AiredAt!.Value))
            .OrderBy(pair => pair.AiredAtUtc)
            .ToList();
        var (trailingShiftDays, trailingCount) = LearnTrailingShift(anidbPairs, options.MinimumSamples);
        var anidbOffset = LearnOffset(anidbPairs.Take(anidbPairs.Count - trailingCount), options.Window, options.MinimumSamples);

        if (options.AnchorOnFirstOriginalAiring)
        {
            var anchoredPairs = releases
                .Where(sample => sample.FirstOriginalAiringAt.HasValue)
                .Select(sample => (AnchorUtc: sample.FirstOriginalAiringAt!.Value, AiredAtUtc: sample.AiredAt!.Value))
                .OrderBy(pair => pair.AiredAtUtc)
                .ToList();
            if (anchoredPairs.Count >= options.MinimumSamples && LearnOffset(anchoredPairs, options.Window, options.MinimumSamples) is { } anchoredOffset)
                return new AiringScheduleProfile(AiringAnchor.FirstOriginalAiring, anchoredOffset, 0, hiatusFrom, lastEstimableEpisode, anidbOffset);
        }

        return new AiringScheduleProfile(AiringAnchor.AnidbDate, anidbOffset, trailingShiftDays, hiatusFrom, lastEstimableEpisode, anidbOffset);
    }

    /// <summary>
    /// Estimate when one episode airs on the schedule a profile belongs to.
    /// </summary>
    /// <param name="profile">The schedule's profile.</param>
    /// <param name="target">The episode the schedule has no airing for.</param>
    /// <returns>The estimate, or <see langword="null"/> when the schedule can't estimate that episode.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="profile"/> or <paramref name="target"/> is <see langword="null"/>.</exception>
    public static AiringEstimate? EstimateAiring(AiringScheduleProfile profile, AiringEstimateTarget target)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(target);

        // Specials are never estimated, and coverage stops extrapolation: a
        // finished schedule for episodes 1-12 estimates nothing for 13-24.
        if (!target.IsNormalEpisode)
            return null;
        if (profile.LastEstimableEpisode is { } lastEpisode && (lastEpisode is 0 || target.EpisodeNumber > lastEpisode))
            return null;

        DateTime slot;
        DateTime? withoutShift = null;
        if (profile.Anchor is AiringAnchor.FirstOriginalAiring && target.FirstOriginalAiringAt is { } originalAiring && profile.Offset is { } anchoredOffset)
        {
            // A simulpub follows the broadcast, so a delayed broadcast moves it.
            slot = originalAiring + anchoredOffset;
        }
        else
        {
            // Without a real Original airing to anchor on, fall back to the
            // AniDB date: an estimate is never anchored on another estimate.
            var offset = profile.Anchor is AiringAnchor.AnidbDate ? profile.Offset : profile.AnidbOffset;
            if (offset is not { } anidbOffset || target.AnidbAirDate is not { } anidbAirDate)
                return null;

            slot = GetMidnightUtc(anidbAirDate) + anidbOffset;
            if (profile.Anchor is AiringAnchor.AnidbDate && profile.TrailingShiftDays is not 0)
            {
                withoutShift = slot;
                slot += TimeSpan.FromDays(profile.TrailingShiftDays);
            }
        }

        // The schedule is on hiatus from here on, so it knows the slot the
        // episode would have had, but not when it airs.
        if (profile.HiatusFrom is { } hiatusFrom && slot >= hiatusFrom)
            return new AiringEstimate(target.EpisodeKey, null, slot);

        return new AiringEstimate(target.EpisodeKey, slot, withoutShift);
    }

    /// <summary>
    /// Reduce every link set to its earliest member, so a double slot counts once
    /// wherever releases are counted.
    /// </summary>
    /// <param name="samples">The schedule's own airings.</param>
    /// <returns>One sample per release.</returns>
    private static List<AiringProfileSample> CollapseLinkSets(IReadOnlyList<AiringProfileSample> samples)
    {
        var releases = new List<AiringProfileSample>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var sample in samples)
        {
            if (sample.LinkKey is not { } linkKey)
            {
                releases.Add(sample);
                continue;
            }

            if (!seen.Add(linkKey))
                continue;

            releases.Add(samples
                .Where(entry => string.Equals(entry.LinkKey, linkKey, StringComparison.Ordinal))
                .OrderBy(entry => entry.AiredAt ?? DateTime.MaxValue)
                .First());
        }

        return releases;
    }

    /// <summary>
    /// Learn whether the latest airings sit a whole number of days away from the
    /// slot the older ones kept, which is what a run that slipped a week looks
    /// like while AniDB still carries the original dates.
    /// </summary>
    /// <param name="pairs">The anchor and air time of every sample, oldest first.</param>
    /// <param name="minimumSamples">How many samples are needed on either side before a shift is trusted.</param>
    /// <returns>The shift in whole days, and how many of the latest samples it covers.</returns>
    private static (int Days, int Count) LearnTrailingShift(IReadOnlyList<(DateTime AnchorUtc, DateTime AiredAtUtc)> pairs, int minimumSamples)
    {
        var minimum = Math.Max(minimumSamples, 2);
        var offsets = pairs.Select(pair => pair.AiredAtUtc - pair.AnchorUtc).ToList();
        var shift = (Days: 0, Count: 0);
        for (var count = 2; count <= offsets.Count - minimum; count++)
        {
            var older = offsets.Take(offsets.Count - count).ToList();
            var median = GetMedian(older);
            var trailing = offsets.Skip(offsets.Count - count).ToList();
            var days = (int)Math.Round((trailing[0] - median).TotalDays);
            if (days is 0)
                continue;

            var candidate = TimeSpan.FromDays(days);
            if (trailing.All(offset => (offset - median - candidate).Duration() <= TimeSpan.FromHours(12)))
                shift = (days, count);
        }

        return shift;
    }

    #endregion

    #region Cadence

    /// <summary>
    /// Find the breaks a line of airings already had the first time it was
    /// fetched, which no write can infer because nothing moved. Gaps are measured
    /// between releases rather than episodes: a link set, or a set of airings at
    /// the same time, is one release timed at its earliest member, so a
    /// double-length premiere shifts nothing and a season released at once has no
    /// cadence at all.
    /// </summary>
    /// <param name="airings">One schedule's own airings.</param>
    /// <param name="options">Optional. The limits to measure within. Defaults to the service's own.</param>
    /// <returns>Every release that arrived after a gap of a whole skipped slot or more, oldest first.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="airings"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<AiringCadenceBreak> FindCadenceBreaks(IEnumerable<AiringProfileSample> airings, AiringCadenceOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(airings);
        options ??= new AiringCadenceOptions();

        var releases = new List<(DateTime AiredAt, List<string> EpisodeKeys)>();
        var units = new Dictionary<string, List<AiringProfileSample>>(StringComparer.Ordinal);
        foreach (var airing in airings
            .Where(airing => airing is not null && airing.AiredAt.HasValue)
            .Where(airing => options.LastEpisodeNumber is not { } last || airing.EpisodeNumber is not { } number || number <= last)
            .OrderBy(airing => airing.AiredAt!.Value))
        {
            // A link set, or a set of airings sharing a slot, is one release.
            var key = airing.LinkKey ?? airing.AiredAt!.Value.Ticks.ToString();
            if (!units.TryGetValue(key, out var unit))
                units[key] = unit = [];

            unit.Add(airing);
        }

        foreach (var unit in units.Values)
            releases.Add((unit.Min(airing => airing.AiredAt!.Value), unit.Select(airing => airing.EpisodeKey).ToList()));

        releases.Sort((left, right) => left.AiredAt.CompareTo(right.AiredAt));
        if (releases.Count < Math.Max(options.MinimumReleases, 3))
            return [];

        var gaps = new List<TimeSpan>();
        for (var index = 1; index < releases.Count; index++)
            gaps.Add(releases[index].AiredAt - releases[index - 1].AiredAt);

        // A split cour leaves a season-long gap, which is neither the cadence nor
        // a break in it.
        var measured = gaps.Where(gap => gap < options.MaximumGap).ToList();
        if (measured.Count is 0)
            return [];

        var cadence = GetMedian(measured);
        if (cadence <= TimeSpan.Zero)
            return [];

        var breaks = new List<AiringCadenceBreak>();
        for (var index = 1; index < releases.Count; index++)
        {
            var gap = releases[index].AiredAt - releases[index - 1].AiredAt;
            if (gap >= options.MaximumGap || gap + options.Tolerance < cadence * 2)
                continue;

            var skipped = (int)Math.Round(gap / cadence) - 1;
            if (skipped < 1)
                continue;

            breaks.Add(new AiringCadenceBreak(
                releases[index].AiredAt,
                releases[index - 1].AiredAt + cadence,
                cadence,
                skipped,
                releases[index].EpisodeKeys
            ));
        }

        return breaks;
    }

    #endregion
}
