using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Utilities;
using Shoko.Server.Utilities.Airing;
using Xunit;

namespace Shoko.Tests.Utilities;

public class AiringScheduleUtilityTests
{
    #region Helpers

    /// <summary>
    /// The nth weekly slot of a Tuesday 15:30 UTC run, counting from zero.
    /// </summary>
    private static DateTime Week(int index)
        => new DateTime(2026, 4, 28, 15, 30, 0, DateTimeKind.Utc).AddDays(7 * index);

    private static ExistingAiring Existing(
        int episode,
        DateTime? airedAt,
        DateTime? originalAiredAt = null,
        bool isDelayed = false,
        string? linkKey = null,
        string? key = null
    )
        => new()
        {
            Key = key ?? $"ep{episode}",
            EpisodeKey = $"AniDB:{episode}",
            EpisodeNumber = episode,
            AiredAt = airedAt,
            OriginalAiredAt = originalAiredAt,
            IsDelayed = isDelayed,
            LinkKey = linkKey,
        };

    private static SubmittedAiring Submitted(
        int episode,
        DateTime? airedAt,
        string? key = null,
        DateTime? originalAiredAt = null,
        bool? isDelayed = null
    )
        => new()
        {
            Key = key ?? $"ep{episode}",
            EpisodeKey = $"AniDB:{episode}",
            AiredAt = airedAt,
            OriginalAiredAt = originalAiredAt,
            IsDelayed = isDelayed,
        };

    private static InferredAiring Saved(AiringInferenceResult result, string key)
        => Assert.Single(result.ToSave, airing => airing.Key == key);

    private static AiringProfileSample Sample(
        int episode,
        DateTime? airedAt,
        DateTime? anidbAirDate = null,
        DateTime? originalAiredAt = null,
        bool isDelayed = false,
        string? linkKey = null,
        DateTime? firstOriginalAiringAt = null
    )
        => new()
        {
            EpisodeKey = $"AniDB:{episode}",
            EpisodeNumber = episode,
            AiredAt = airedAt,
            AnidbAirDate = anidbAirDate,
            OriginalAiredAt = originalAiredAt,
            IsDelayed = isDelayed,
            LinkKey = linkKey,
            FirstOriginalAiringAt = firstOriginalAiringAt,
        };

    /// <summary>
    /// A run of weekly airings in the schedule's own slot, each with the matching
    /// AniDB date.
    /// </summary>
    private static List<AiringProfileSample> WeeklySamples(int from, int count, TimeSpan? offset = null)
        => Enumerable.Range(from, count)
            .Select(index => Sample(index + 1, Week(index) + (offset ?? TimeSpan.Zero), anidbAirDate: Week(index).Date))
            .ToList();

    private static AiringEstimateTarget Target(
        int episode,
        DateTime? anidbAirDate = null,
        DateTime? firstOriginalAiringAt = null,
        bool isNormalEpisode = true
    )
        => new()
        {
            EpisodeKey = $"AniDB:{episode}",
            EpisodeNumber = episode,
            AnidbAirDate = anidbAirDate,
            FirstOriginalAiringAt = firstOriginalAiringAt,
            IsNormalEpisode = isNormalEpisode,
        };

    #endregion

    #region Keys

    [Fact]
    public void GetDerivedScheduleKey_IgnoresTrackOrderAndDuplicates()
    {
        var channelID = new Guid("6b1ef4b0-7d1b-4a3e-9f2b-2a0a0f6f8c11");
        var original = new AiringTrackData(AiringKind.Original, "ja");
        var subtitled = new AiringTrackData(AiringKind.Subtitled, "en");
        var first = AiringScheduleUtility.GetDerivedScheduleKey(channelID, [subtitled, original]);
        var second = AiringScheduleUtility.GetDerivedScheduleKey(channelID, [original with { LanguageCode = "JA" }, subtitled, original]);

        Assert.Equal(first, second);
        Assert.Equal($"{channelID:D}:Original/ja,Subtitled/en", first);
    }

    [Fact]
    public void GetDerivedScheduleKey_MarksAChannellessScheduleAndKeepsTheCountryCode()
    {
        var key = AiringScheduleUtility.GetDerivedScheduleKey(null, [new AiringTrackData(AiringKind.Dubbed, "pt", "br")]);

        Assert.Equal("-:Dubbed/pt/BR", key);
    }

    [Fact]
    public void GetDerivedScheduleKey_NeedsATrack()
    {
        Assert.Throws<ArgumentNullException>(() => AiringScheduleUtility.GetDerivedScheduleKey(null, null!));
        Assert.Throws<ArgumentException>(() => AiringScheduleUtility.GetDerivedScheduleKey(null, []));
    }

    [Fact]
    public void GetDerivedAiringKey_IsTheEpisodeKey()
    {
        Assert.Equal("AniDB:4242", AiringScheduleUtility.GetDerivedAiringKey(DataSource.AniDB, "4242"));
        Assert.Throws<ArgumentException>(() => AiringScheduleUtility.GetDerivedAiringKey(DataSource.AniDB, " "));
    }

    [Fact]
    public void GetDerivedSlotAiringKey_AppendsTheEpisodeToTheSlot()
    {
        Assert.Equal("chid-1234:1", AiringScheduleUtility.GetDerivedSlotAiringKey("chid-1234", 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => AiringScheduleUtility.GetDerivedSlotAiringKey("chid-1234", -1));
    }

    #endregion

    #region Writes & delay inference

    [Fact]
    public void InferAirings_InsertsNewAirings()
    {
        var result = AiringScheduleUtility.InferAirings([], [Submitted(1, Week(0)), Submitted(2, Week(1))], Week(-1));

        Assert.Empty(result.ToDelete);
        Assert.Equal(2, result.ToSave.Count);
        Assert.All(result.ToSave, airing =>
        {
            Assert.True(airing.IsNew);
            Assert.Null(airing.OriginalAiredAt);
            Assert.False(airing.IsDelayed);
        });
    }

    [Fact]
    public void InferAirings_ThrowsOnDuplicateKeys()
    {
        Assert.Throws<ArgumentException>(() => AiringScheduleUtility.InferAirings([], [Submitted(1, Week(0)), Submitted(2, Week(1), key: "ep1")], Week(-1)));
        Assert.Throws<ArgumentException>(() => AiringScheduleUtility.InferAirings([Existing(1, Week(0)), Existing(2, Week(1), key: "ep1")], [], Week(-1)));
    }

    [Fact]
    public void InferAirings_ResubmittingTheSameLineIsNeverADelay()
    {
        // A track change on the schedule submits the same airings again.
        var existing = new[] { Existing(4, Week(0)), Existing(5, Week(1), originalAiredAt: Week(0), isDelayed: true) };
        var submitted = new[] { Submitted(4, Week(0)), Submitted(5, Week(1)) };

        var result = AiringScheduleUtility.InferAirings(existing, submitted, Week(0));

        Assert.Empty(result.ToDelete);
        var episode4 = Saved(result, "ep4");
        Assert.Null(episode4.OriginalAiredAt);
        Assert.False(episode4.IsDelayed);

        // An airing that didn't move keeps what it already knew.
        var episode5 = Saved(result, "ep5");
        Assert.Equal(Week(0), episode5.OriginalAiredAt);
        Assert.True(episode5.IsDelayed);
    }

    [Fact]
    public void InferAirings_OneWeekBreakFlagsOnlyTheEpisodeThatCausedIt()
    {
        var existing = new[] { Existing(4, Week(0)), Existing(5, Week(1)), Existing(6, Week(2)), Existing(7, Week(3)), Existing(8, Week(4)) };
        var submitted = new[] { Submitted(4, Week(0)), Submitted(5, Week(2)), Submitted(6, Week(3)), Submitted(7, Week(4)), Submitted(8, Week(5)) };

        var result = AiringScheduleUtility.InferAirings(existing, submitted, Week(0).AddHours(1));

        Assert.Empty(result.ToDelete);
        var episode4 = Saved(result, "ep4");
        Assert.Equal(Week(0), episode4.AiredAt);
        Assert.Null(episode4.OriginalAiredAt);
        Assert.False(episode4.IsDelayed);

        var episode5 = Saved(result, "ep5");
        Assert.Equal(Week(2), episode5.AiredAt);
        Assert.Equal(Week(1), episode5.OriginalAiredAt);
        Assert.True(episode5.IsDelayed);

        // Every moved airing keeps its first slot, but only the cause is delayed.
        foreach (var (key, original, current) in new[] { ("ep6", Week(2), Week(3)), ("ep7", Week(3), Week(4)), ("ep8", Week(4), Week(5)) })
        {
            var airing = Saved(result, key);
            Assert.Equal(current, airing.AiredAt);
            Assert.Equal(original, airing.OriginalAiredAt);
            Assert.False(airing.IsDelayed);
        }
    }

    [Fact]
    public void InferAirings_BackToBackBreaksEachFlagTheirOwnCause()
    {
        var existing = new[] { Existing(5, Week(1)), Existing(6, Week(2)), Existing(7, Week(3)), Existing(8, Week(4)) };
        // Episodes 5 and 6 slip a week, then 7 and 8 slip another one on top.
        var submitted = new[] { Submitted(5, Week(2)), Submitted(6, Week(3)), Submitted(7, Week(5)), Submitted(8, Week(6)) };

        var result = AiringScheduleUtility.InferAirings(existing, submitted, Week(0));

        Assert.True(Saved(result, "ep5").IsDelayed);
        Assert.False(Saved(result, "ep6").IsDelayed);
        Assert.True(Saved(result, "ep7").IsDelayed);
        Assert.False(Saved(result, "ep8").IsDelayed);
    }

    [Fact]
    public void InferAirings_AMoveUnderADayIsACorrection()
    {
        var result = AiringScheduleUtility.InferAirings([Existing(5, Week(1))], [Submitted(5, Week(1).AddMinutes(5))], Week(0));

        var airing = Saved(result, "ep5");
        Assert.Equal(Week(1).AddMinutes(5), airing.AiredAt);
        Assert.Null(airing.OriginalAiredAt);
        Assert.False(airing.IsDelayed);
    }

    [Fact]
    public void InferAirings_KeepsTheFirstSlotThroughRepeatedMoves()
    {
        var existing = new[] { Existing(5, Week(2), originalAiredAt: Week(1), isDelayed: true) };

        var result = AiringScheduleUtility.InferAirings(existing, [Submitted(5, Week(3))], Week(0));

        var airing = Saved(result, "ep5");
        Assert.Equal(Week(3), airing.AiredAt);
        Assert.Equal(Week(1), airing.OriginalAiredAt);
        Assert.True(airing.IsDelayed);
    }

    [Fact]
    public void InferAirings_MovedEarlierKeepsTheFirstSlotWithoutFlagging()
    {
        var result = AiringScheduleUtility.InferAirings([Existing(5, Week(2))], [Submitted(5, Week(1))], Week(0));

        var airing = Saved(result, "ep5");
        Assert.Equal(Week(1), airing.AiredAt);
        Assert.Equal(Week(2), airing.OriginalAiredAt);
        Assert.False(airing.IsDelayed);
    }

    [Fact]
    public void InferAirings_RemovedFutureAiringsBecomeAHiatus()
    {
        var existing = new[] { Existing(4, Week(0)), Existing(5, Week(1)), Existing(6, Week(2)) };

        var result = AiringScheduleUtility.InferAirings(existing, [Submitted(4, Week(0))], Week(0).AddHours(1));

        Assert.Empty(result.ToDelete);
        var episode5 = Saved(result, "ep5");
        Assert.Null(episode5.AiredAt);
        Assert.Equal(Week(1), episode5.OriginalAiredAt);
        Assert.True(episode5.IsDelayed);

        var episode6 = Saved(result, "ep6");
        Assert.Null(episode6.AiredAt);
        Assert.Equal(Week(2), episode6.OriginalAiredAt);
        Assert.False(episode6.IsDelayed);
    }

    [Fact]
    public void InferAirings_RemovedPastAiringsAreDeleted()
    {
        var existing = new[] { Existing(4, Week(0)), Existing(5, Week(1)) };

        var result = AiringScheduleUtility.InferAirings(existing, [Submitted(5, Week(1))], Week(0).AddHours(1));

        var deleted = Assert.Single(result.ToDelete);
        Assert.Equal("ep4", deleted.Key);
    }

    [Fact]
    public void InferAirings_RemovedAiringsOnAFinishedScheduleOrPastItsCoverageAreDeleted()
    {
        var existing = new[] { Existing(5, Week(1)), Existing(13, Week(9)) };

        var finished = AiringScheduleUtility.InferAirings(existing, [], Week(0), new AiringInferenceOptions { IsFinished = true });
        Assert.Equal(2, finished.ToDelete.Count);
        Assert.Empty(finished.ToSave);

        var covered = AiringScheduleUtility.InferAirings(existing, [], Week(0), new AiringInferenceOptions { LastEpisodeNumber = 12 });
        var deleted = Assert.Single(covered.ToDelete);
        Assert.Equal("ep13", deleted.Key);
        Assert.Equal("ep5", Assert.Single(covered.ToSave).Key);
    }

    [Fact]
    public void InferAirings_ARecreatedEntryUnderANewKeyKeepsTheOldRow()
    {
        var existing = new[] { Existing(5, null, originalAiredAt: Week(1), isDelayed: true, key: "anilist-11") };

        var result = AiringScheduleUtility.InferAirings(existing, [Submitted(5, Week(4), key: "anilist-99")], Week(0));

        Assert.Empty(result.ToDelete);
        var airing = Saved(result, "anilist-99");
        Assert.Equal("anilist-11", airing.ExistingKey);
        Assert.True(airing.IsReKeyed);
        // Returned to a slot: the row keeps what it already knew.
        Assert.Equal(Week(4), airing.AiredAt);
        Assert.Equal(Week(1), airing.OriginalAiredAt);
        Assert.True(airing.IsDelayed);
    }

    [Fact]
    public void InferAirings_PairsARecreatedEntryWithTheSlotlessRowFirst()
    {
        var existing = new[]
        {
            Existing(5, Week(1), key: "old-slot"),
            Existing(5, null, originalAiredAt: Week(2), isDelayed: true, key: "old-hiatus"),
        };

        var result = AiringScheduleUtility.InferAirings(existing, [Submitted(5, Week(3), key: "new")], Week(2).AddHours(1));

        Assert.Equal("old-hiatus", Saved(result, "new").ExistingKey);
        Assert.Equal("old-slot", Assert.Single(result.ToDelete).Key);
    }

    [Fact]
    public void InferAirings_ProviderValuesWin()
    {
        var existing = new[] { Existing(5, Week(1)), Existing(6, Week(2)) };
        var submitted = new[]
        {
            Submitted(5, Week(2), originalAiredAt: Week(0), isDelayed: false),
            Submitted(6, Week(3), isDelayed: true),
        };

        var result = AiringScheduleUtility.InferAirings(existing, submitted, Week(0));

        var episode5 = Saved(result, "ep5");
        Assert.Equal(Week(0), episode5.OriginalAiredAt);
        Assert.False(episode5.IsDelayed);
        Assert.True(Saved(result, "ep6").IsDelayed);
    }

    [Fact]
    public void InferAirings_ASlotlessAiringIsKeptUntilSomethingContradictsIt()
    {
        var existing = new[] { Existing(5, null, originalAiredAt: Week(1), isDelayed: true) };

        var kept = AiringScheduleUtility.InferAirings(existing, [], Week(2));
        Assert.Empty(kept.ToSave);
        Assert.Empty(kept.ToDelete);

        var superseded = AiringScheduleUtility.InferAirings(existing, [], Week(2), new AiringInferenceOptions
        {
            SupersededEpisodeKeys = new HashSet<string> { "AniDB:5" },
        });
        Assert.Equal("ep5", Assert.Single(superseded.ToDelete).Key);

        // Another channel's episode doesn't supersede it.
        var other = AiringScheduleUtility.InferAirings(existing, [], Week(2), new AiringInferenceOptions
        {
            SupersededEpisodeKeys = new HashSet<string> { "AniDB:6" },
        });
        Assert.Empty(other.ToDelete);
    }

    [Fact]
    public void InferAirings_ASlotlessAiringExpiresAtTheOwnersFirstWriteAfterTheWindow()
    {
        var existing = new[] { Existing(5, null, originalAiredAt: Week(1), isDelayed: true) };

        var early = AiringScheduleUtility.InferAirings(existing, [], Week(1).AddDays(179));
        Assert.Empty(early.ToDelete);

        var expired = AiringScheduleUtility.InferAirings(existing, [], Week(1).AddDays(181));
        Assert.Equal("ep5", Assert.Single(expired.ToDelete).Key);
    }

    [Fact]
    public void InferAirings_WithoutInferenceStoresWhatWasSubmittedAndDeletesTheRest()
    {
        var existing = new[] { Existing(5, Week(1)), Existing(6, Week(2)) };
        var submitted = new[] { Submitted(5, null, originalAiredAt: Week(1), isDelayed: true) };

        var result = AiringScheduleUtility.InferAirings(existing, submitted, Week(0), new AiringInferenceOptions { InferDelays = false });

        var airing = Saved(result, "ep5");
        Assert.Null(airing.AiredAt);
        Assert.Equal(Week(1), airing.OriginalAiredAt);
        Assert.True(airing.IsDelayed);
        // A removed future airing is not a hiatus when the provider reports its own.
        Assert.Equal("ep6", Assert.Single(result.ToDelete).Key);
    }

    [Fact]
    public void InferAirings_ALinkSetIsOneUnitAndIsFlaggedTogether()
    {
        var existing = new[]
        {
            Existing(5, Week(1), linkKey: "ep5"),
            Existing(6, Week(1), linkKey: "ep5"),
            Existing(7, Week(2)),
        };
        var submitted = new[] { Submitted(5, Week(2)), Submitted(6, Week(2)), Submitted(7, Week(3)) };

        var result = AiringScheduleUtility.InferAirings(existing, submitted, Week(0));

        Assert.True(Saved(result, "ep5").IsDelayed);
        Assert.True(Saved(result, "ep6").IsDelayed);
        // The set counts once, so the episode behind it merely shifted.
        Assert.False(Saved(result, "ep7").IsDelayed);
        Assert.Equal("ep5", Saved(result, "ep6").LinkKey);
    }

    [Fact]
    public void InferAirings_AStaleLinkMemberIsUnlinked()
    {
        var existing = new[]
        {
            Existing(5, Week(1), linkKey: "ep5"),
            Existing(6, Week(1), linkKey: "ep5"),
        };
        var submitted = new[] { Submitted(5, Week(1)), Submitted(6, Week(2)) };

        var result = AiringScheduleUtility.InferAirings(existing, submitted, Week(0));

        Assert.Equal("ep5", Saved(result, "ep5").LinkKey);
        Assert.Null(Saved(result, "ep6").LinkKey);
    }

    [Fact]
    public void InferAirings_AMemberThatLosesItsSlotAloneIsUnlinked()
    {
        var existing = new[]
        {
            Existing(5, Week(1), linkKey: "ep5"),
            Existing(6, Week(1), linkKey: "ep5"),
        };

        // Only episode 6 is dropped, and its slot is still ahead of us.
        var result = AiringScheduleUtility.InferAirings(existing, [Submitted(5, Week(1))], Week(0));

        Assert.Null(Saved(result, "ep6").LinkKey);
        Assert.True(Saved(result, "ep6").IsDelayed);
    }

    #endregion

    #region Delta writes

    [Fact]
    public void MergeAirings_LeavesWhatItDoesNotMentionOutOfTheResult()
    {
        var existing = new[] { Existing(4, Week(0)), Existing(5, Week(1)), Existing(6, Week(2)) };

        var result = AiringScheduleUtility.MergeAirings(existing, [Submitted(7, Week(3))], [], Week(0));

        // Episodes 4 to 6 are neither stored again nor removed, so the write is
        // exactly the one row it was given.
        Assert.Empty(result.ToDelete);
        var added = Assert.Single(result.ToSave);
        Assert.Equal("ep7", added.Key);
        Assert.True(added.IsNew);
    }

    [Fact]
    public void MergeAirings_ReadsAStatedRemovalTheWayAnOmissionIsRead()
    {
        var existing = new[] { Existing(4, Week(0)), Existing(5, Week(1)), Existing(6, Week(2)) };

        // Episode 6's slot is still ahead, so it is a hiatus; episode 4's has
        // passed, so it is history.
        var hiatus = AiringScheduleUtility.MergeAirings(existing, [], ["ep6"], Week(1).AddHours(1));
        Assert.Empty(hiatus.ToDelete);
        var kept = Assert.Single(hiatus.ToSave);
        Assert.Null(kept.AiredAt);
        Assert.Equal(Week(2), kept.OriginalAiredAt);

        var history = AiringScheduleUtility.MergeAirings(existing, [], ["ep4"], Week(1).AddHours(1));
        Assert.Empty(history.ToSave);
        Assert.Equal("ep4", Assert.Single(history.ToDelete).Key);
    }

    [Fact]
    public void MergeAirings_CountsUntouchedAiringsAsPartOfTheLine()
    {
        var existing = new[] { Existing(4, Week(0)), Existing(5, Week(1)), Existing(6, Week(2)), Existing(7, Week(3)) };

        // Two breaks of a week each, with an airing that did not move between
        // them. Both causes are flagged, because the untouched airing in the
        // middle ends the first run rather than extending it.
        var result = AiringScheduleUtility.MergeAirings(existing, [Submitted(5, Week(2)), Submitted(7, Week(4))], [], Week(0));

        Assert.Empty(result.ToDelete);
        Assert.True(Saved(result, "ep5").IsDelayed);
        Assert.True(Saved(result, "ep7").IsDelayed);
    }

    [Fact]
    public void MergeAirings_ThrowsOnARemovalItCannotPlace()
    {
        var existing = new[] { Existing(4, Week(0)) };

        Assert.Throws<ArgumentException>(() => AiringScheduleUtility.MergeAirings(existing, [], ["ep9"], Week(0)));
        Assert.Throws<ArgumentException>(() => AiringScheduleUtility.MergeAirings(existing, [Submitted(4, Week(1))], ["ep4"], Week(0)));
    }

    #endregion

    #region Estimation | Offsets

    private static (DateTime, DateTime) OffsetSample(int day, int hour, int minute = 0)
        => (new DateTime(2026, 7, day), new DateTime(2026, 7, day, hour, minute, 0, DateTimeKind.Utc));

    [Fact]
    public void LearnAirTimeOffset_NeedsAtLeastTwoSamples()
    {
        Assert.Null(AiringScheduleUtility.LearnAirTimeOffset([]));
        Assert.Null(AiringScheduleUtility.LearnAirTimeOffset([OffsetSample(2, 16, 28)]));
    }

    [Fact]
    public void LearnAirTimeOffset_ReturnsTheSharedSlot()
    {
        var offset = AiringScheduleUtility.LearnAirTimeOffset([OffsetSample(2, 16, 28), OffsetSample(9, 16, 28), OffsetSample(16, 16, 28)]);

        Assert.Equal(new TimeSpan(16, 28, 0), offset);
    }

    [Fact]
    public void LearnAirTimeOffset_IgnoresAOneOffSlotThroughTheMedian()
    {
        // Episode 9 aired in a different slot; the median still lands on the regular one.
        var offset = AiringScheduleUtility.LearnAirTimeOffset([OffsetSample(2, 16, 28), OffsetSample(9, 3, 0), OffsetSample(16, 16, 28)]);

        Assert.Equal(new TimeSpan(16, 28, 0), offset);
    }

    [Fact]
    public void LearnAirTimeOffset_CrossesMidnightWhenTheSlotIsOnTheNextUtcDay()
    {
        // A late-night JST slot lands on the following UTC day; the offset is simply more than a day.
        var samples = new List<(DateTime, DateTime)>
        {
            (new DateTime(2026, 7, 2), new DateTime(2026, 7, 3, 1, 30, 0, DateTimeKind.Utc)),
            (new DateTime(2026, 7, 9), new DateTime(2026, 7, 10, 1, 30, 0, DateTimeKind.Utc)),
        };

        var offset = AiringScheduleUtility.LearnAirTimeOffset(samples);

        Assert.Equal(new TimeSpan(25, 30, 0), offset);
        Assert.Equal(new DateTime(2026, 7, 24, 1, 30, 0, DateTimeKind.Utc), AiringScheduleUtility.EstimateAirTime(new DateTime(2026, 7, 23), offset!.Value));
    }

    [Fact]
    public void LearnAirTimeOffset_OnlyLooksAtTheMostRecentWindow()
    {
        // Twelve old episodes in one slot, then the series moved; with a window of two only the new slot counts.
        var samples = new List<(DateTime, DateTime)>();
        for (var day = 1; day <= 12; day++)
            samples.Add((new DateTime(2026, 1, day), new DateTime(2026, 1, day, 9, 0, 0, DateTimeKind.Utc)));
        samples.Add(OffsetSample(2, 16, 28));
        samples.Add(OffsetSample(9, 16, 28));

        Assert.Equal(new TimeSpan(16, 28, 0), AiringScheduleUtility.LearnAirTimeOffset(samples, window: 2));
        Assert.Equal(new TimeSpan(9, 0, 0), AiringScheduleUtility.LearnAirTimeOffset(samples, window: 14));
    }

    [Fact]
    public void EstimateAirTime_IsUtc()
    {
        var estimate = AiringScheduleUtility.EstimateAirTime(new DateTime(2026, 7, 23), new TimeSpan(16, 28, 0));

        Assert.Equal(DateTimeKind.Utc, estimate.Kind);
        Assert.Equal(new DateTime(2026, 7, 23, 16, 28, 0), estimate);
    }

    #endregion

    #region Estimation | Profiles

    [Fact]
    public void LearnProfile_LearnsTheSlotFromTheAnidbDate()
    {
        var profile = AiringScheduleUtility.LearnProfile(WeeklySamples(0, 4));

        Assert.Equal(AiringAnchor.AnidbDate, profile.Anchor);
        Assert.Equal(new TimeSpan(15, 30, 0), profile.Offset);
        Assert.Equal(0, profile.TrailingShiftDays);
        Assert.Null(profile.HiatusFrom);

        var estimate = AiringScheduleUtility.EstimateAiring(profile, Target(5, anidbAirDate: Week(4).Date));
        Assert.NotNull(estimate);
        Assert.Equal(Week(4), estimate.AiredAt);
        Assert.Null(estimate.OriginalAiredAt);
    }

    [Fact]
    public void LearnProfile_NeedsTwoSamplesBeforeItEstimatesAnything()
    {
        var profile = AiringScheduleUtility.LearnProfile([Sample(1, Week(0), anidbAirDate: Week(0).Date)]);

        Assert.Null(profile.Offset);
        Assert.Null(AiringScheduleUtility.EstimateAiring(profile, Target(2, anidbAirDate: Week(1).Date)));
    }

    [Fact]
    public void LearnProfile_SkipsDelayedAirings()
    {
        var samples = new[]
        {
            Sample(1, Week(0), anidbAirDate: Week(0).Date),
            Sample(2, Week(1), anidbAirDate: Week(1).Date),
            Sample(3, Week(2).AddHours(4), anidbAirDate: Week(2).Date, originalAiredAt: Week(2), isDelayed: true),
            Sample(4, Week(3).AddHours(4), anidbAirDate: Week(3).Date, originalAiredAt: Week(3), isDelayed: true),
        };

        var profile = AiringScheduleUtility.LearnProfile(samples);

        Assert.Equal(new TimeSpan(15, 30, 0), profile.Offset);
    }

    [Fact]
    public void LearnProfile_ALinkSetCountsOnce()
    {
        // Three episodes released in one slot are one sample, which is too few to learn from.
        var samples = new[]
        {
            Sample(1, Week(0), anidbAirDate: Week(0).Date, linkKey: "ep1"),
            Sample(2, Week(0), anidbAirDate: Week(0).Date, linkKey: "ep1"),
            Sample(3, Week(0), anidbAirDate: Week(0).Date, linkKey: "ep1"),
        };

        Assert.Null(AiringScheduleUtility.LearnProfile(samples).Offset);
    }

    [Fact]
    public void LearnProfile_LearnsATrailingShiftOfSevenDays()
    {
        var samples = new List<AiringProfileSample>();
        for (var index = 0; index < 6; index++)
            samples.Add(Sample(index + 1, Week(index), anidbAirDate: Week(index).Date));

        // The run slipped a week while AniDB still carries the original dates.
        samples.Add(Sample(7, Week(7), anidbAirDate: Week(6).Date));
        samples.Add(Sample(8, Week(8), anidbAirDate: Week(7).Date));

        var profile = AiringScheduleUtility.LearnProfile(samples);

        Assert.Equal(7, profile.TrailingShiftDays);
        Assert.Equal(new TimeSpan(15, 30, 0), profile.Offset);

        var estimate = AiringScheduleUtility.EstimateAiring(profile, Target(9, anidbAirDate: Week(8).Date));
        Assert.NotNull(estimate);
        Assert.Equal(Week(9), estimate.AiredAt);
        Assert.Equal(Week(8), estimate.OriginalAiredAt);
    }

    [Fact]
    public void LearnProfile_LearnsASteadyFourteenDayDubLagAsTheOffset()
    {
        var samples = Enumerable.Range(0, 4)
            .Select(index => Sample(index + 1, Week(index).AddDays(14), anidbAirDate: Week(index).Date))
            .ToList();

        var profile = AiringScheduleUtility.LearnProfile(samples);

        Assert.Equal(AiringAnchor.AnidbDate, profile.Anchor);
        Assert.Equal(TimeSpan.FromDays(14) + new TimeSpan(15, 30, 0), profile.Offset);
        Assert.Equal(0, profile.TrailingShiftDays);

        var estimate = AiringScheduleUtility.EstimateAiring(profile, Target(5, anidbAirDate: Week(4).Date));
        Assert.Equal(Week(4).AddDays(14), estimate?.AiredAt);
    }

    [Fact]
    public void LearnProfile_AnchorsASubtitledScheduleOnTheOriginalAiring()
    {
        var samples = Enumerable.Range(0, 3)
            .Select(index => Sample(index + 1, Week(index).AddHours(1), anidbAirDate: Week(index).Date, firstOriginalAiringAt: Week(index)))
            .ToList();

        var profile = AiringScheduleUtility.LearnProfile(samples, new AiringProfileOptions { AnchorOnFirstOriginalAiring = true });

        Assert.Equal(AiringAnchor.FirstOriginalAiring, profile.Anchor);
        Assert.Equal(TimeSpan.FromHours(1), profile.Offset);

        // A delayed broadcast moves the simulcast with it.
        var estimate = AiringScheduleUtility.EstimateAiring(profile, Target(4, anidbAirDate: Week(3).Date, firstOriginalAiringAt: Week(3).AddDays(3)));
        Assert.Equal(Week(3).AddDays(3).AddHours(1), estimate?.AiredAt);
    }

    [Fact]
    public void LearnProfile_FallsBackToTheAnidbAnchorWithoutEnoughOriginalAirings()
    {
        var samples = new[]
        {
            Sample(1, Week(0).AddHours(1), anidbAirDate: Week(0).Date, firstOriginalAiringAt: Week(0)),
            Sample(2, Week(1).AddHours(1), anidbAirDate: Week(1).Date),
            Sample(3, Week(2).AddHours(1), anidbAirDate: Week(2).Date),
        };

        var profile = AiringScheduleUtility.LearnProfile(samples, new AiringProfileOptions { AnchorOnFirstOriginalAiring = true });

        Assert.Equal(AiringAnchor.AnidbDate, profile.Anchor);
        Assert.Equal(new TimeSpan(16, 30, 0), profile.Offset);
    }

    [Fact]
    public void EstimateAiring_AnEstimatedOriginalAiringNeverAnchors()
    {
        var samples = Enumerable.Range(0, 3)
            .Select(index => Sample(index + 1, Week(index).AddHours(1), anidbAirDate: Week(index).Date, firstOriginalAiringAt: Week(index)))
            .ToList();
        var profile = AiringScheduleUtility.LearnProfile(samples, new AiringProfileOptions { AnchorOnFirstOriginalAiring = true });

        // Nothing real to anchor on, so the AniDB anchor answers instead.
        var estimate = AiringScheduleUtility.EstimateAiring(profile, Target(4, anidbAirDate: Week(3).Date));

        Assert.Equal(new TimeSpan(16, 30, 0), profile.AnidbOffset);
        Assert.Equal(Week(3).AddHours(1), estimate?.AiredAt);
    }

    [Fact]
    public void EstimateAiring_StopsAtAHiatusOnThatScheduleOnly()
    {
        var samples = new List<AiringProfileSample>();
        for (var index = 0; index < 3; index++)
            samples.Add(Sample(index + 1, Week(index), anidbAirDate: Week(index).Date));
        samples.Add(Sample(4, null, anidbAirDate: Week(3).Date, originalAiredAt: Week(3), isDelayed: true));

        var onHiatus = AiringScheduleUtility.LearnProfile(samples);
        Assert.Equal(Week(3), onHiatus.HiatusFrom);

        var estimate = AiringScheduleUtility.EstimateAiring(onHiatus, Target(5, anidbAirDate: Week(4).Date));
        Assert.NotNull(estimate);
        Assert.Null(estimate.AiredAt);
        Assert.Equal(Week(4), estimate.OriginalAiredAt);

        // The other station kept airing, so its own estimates are untouched.
        var other = AiringScheduleUtility.LearnProfile(samples.Take(3).Select(sample => sample with { AiredAt = sample.AiredAt!.Value.AddMinutes(30) }));
        Assert.Null(other.HiatusFrom);
        Assert.Equal(Week(4).AddMinutes(30), AiringScheduleUtility.EstimateAiring(other, Target(5, anidbAirDate: Week(4).Date))?.AiredAt);
    }

    [Fact]
    public void EstimateAiring_OneEpisodeCarriesAnEstimatePerSchedule()
    {
        var tokyoMx = AiringScheduleUtility.LearnProfile(WeeklySamples(0, 3));
        var bs11 = AiringScheduleUtility.LearnProfile(WeeklySamples(0, 3, TimeSpan.FromMinutes(30)));

        var target = Target(4, anidbAirDate: Week(3).Date);

        Assert.Equal(Week(3), AiringScheduleUtility.EstimateAiring(tokyoMx, target)?.AiredAt);
        Assert.Equal(Week(3).AddMinutes(30), AiringScheduleUtility.EstimateAiring(bs11, target)?.AiredAt);
    }

    [Fact]
    public void EstimateAiring_NeverEstimatesSpecialsOrAnythingPastTheCoverage()
    {
        var samples = WeeklySamples(0, 3);
        var profile = AiringScheduleUtility.LearnProfile(samples, new AiringProfileOptions { LastEpisodeNumber = 12 });

        Assert.Null(AiringScheduleUtility.EstimateAiring(profile, Target(4, anidbAirDate: Week(3).Date, isNormalEpisode: false)));
        Assert.NotNull(AiringScheduleUtility.EstimateAiring(profile, Target(12, anidbAirDate: Week(11).Date)));
        Assert.Null(AiringScheduleUtility.EstimateAiring(profile, Target(13, anidbAirDate: Week(12).Date)));

        var finished = AiringScheduleUtility.LearnProfile(samples, new AiringProfileOptions { IsFinished = true });
        Assert.Equal(0, finished.LastEstimableEpisode);
        Assert.Null(AiringScheduleUtility.EstimateAiring(finished, Target(4, anidbAirDate: Week(3).Date)));
    }

    [Fact]
    public void EstimateAiring_NeedsAnAnidbDate()
    {
        var profile = AiringScheduleUtility.LearnProfile(WeeklySamples(0, 3));

        Assert.Null(AiringScheduleUtility.EstimateAiring(profile, Target(4)));
    }

    #endregion

    #region Estimation | Cadence

    [Fact]
    public void FindCadenceBreaks_FindsAWeekTheRunSkipped()
    {
        var airings = new List<AiringProfileSample>();
        for (var index = 0; index < 4; index++)
            airings.Add(Sample(index + 1, Week(index)));
        // Episode 5 arrives a fortnight after episode 4.
        for (var index = 0; index < 3; index++)
            airings.Add(Sample(index + 5, Week(index + 5)));

        var breaks = AiringScheduleUtility.FindCadenceBreaks(airings);

        var gap = Assert.Single(breaks);
        Assert.Equal(Week(5), gap.AiredAt);
        Assert.Equal(Week(4), gap.ExpectedAiredAt);
        Assert.Equal(TimeSpan.FromDays(7), gap.Cadence);
        Assert.Equal(1, gap.SkippedSlots);
        Assert.Equal("AniDB:5", Assert.Single(gap.EpisodeKeys));
    }

    [Fact]
    public void FindCadenceBreaks_ADoubleLengthPremiereShiftsNothing()
    {
        var airings = new List<AiringProfileSample> { Sample(1, Week(0)), Sample(2, Week(0)) };
        for (var index = 1; index < 5; index++)
            airings.Add(Sample(index + 2, Week(index)));

        Assert.Empty(AiringScheduleUtility.FindCadenceBreaks(airings));
    }

    [Fact]
    public void FindCadenceBreaks_ALinkSetIsOneRelease()
    {
        var airings = new List<AiringProfileSample>
        {
            Sample(1, Week(0), linkKey: "ep1"),
            // The second half of a double slot, listed half an hour later.
            Sample(2, Week(0).AddMinutes(30), linkKey: "ep1"),
        };
        for (var index = 1; index < 5; index++)
            airings.Add(Sample(index + 2, Week(index)));

        Assert.Empty(AiringScheduleUtility.FindCadenceBreaks(airings));
    }

    [Fact]
    public void FindCadenceBreaks_ASeasonReleasedAtOnceHasNoCadence()
    {
        var airings = Enumerable.Range(1, 12).Select(episode => Sample(episode, Week(0))).ToList();

        Assert.Empty(AiringScheduleUtility.FindCadenceBreaks(airings));
    }

    [Fact]
    public void FindCadenceBreaks_NeedsAFewReleases()
    {
        var airings = new List<AiringProfileSample> { Sample(1, Week(0)), Sample(2, Week(1)), Sample(3, Week(3)) };

        Assert.Empty(AiringScheduleUtility.FindCadenceBreaks(airings));
    }

    [Fact]
    public void FindCadenceBreaks_ASplitCourIsNotABreak()
    {
        var airings = new List<AiringProfileSample>();
        for (var index = 0; index < 6; index++)
            airings.Add(Sample(index + 1, Week(index)));
        // The second cour picks up three months later.
        for (var index = 0; index < 6; index++)
            airings.Add(Sample(index + 7, Week(index + 18)));

        Assert.Empty(AiringScheduleUtility.FindCadenceBreaks(airings));
    }

    [Fact]
    public void FindCadenceBreaks_StopsAtTheCoverage()
    {
        var airings = new List<AiringProfileSample>();
        for (var index = 0; index < 6; index++)
            airings.Add(Sample(index + 1, Week(index)));
        airings.Add(Sample(7, Week(7)));

        Assert.Single(AiringScheduleUtility.FindCadenceBreaks(airings));
        Assert.Empty(AiringScheduleUtility.FindCadenceBreaks(airings, new AiringCadenceOptions { LastEpisodeNumber = 6 }));
    }

    #endregion
}
