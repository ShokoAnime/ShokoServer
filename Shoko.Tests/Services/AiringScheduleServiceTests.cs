using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Services;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Plugin;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Databases;
using Shoko.Server.Models.Airing;
using Shoko.Server.Models.Internal;
using Shoko.Server.Plugin;
using Shoko.Server.Repositories.Cached.Airing;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Server;
using Shoko.Server.Services;
using Shoko.Server.Services.Airing;
using Shoko.Server.Settings;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
///   The airing schedule service is the only thing that writes schedules, airings and channels,
///   and the only thing that reads them back enriched, so everything worth asserting about the
///   feature lives on this seam: who may change what, which airing a caller is handed out of
///   several, what a link set does across writes, and what retention takes away.
/// </summary>
/// <remarks>
///   The service reaches its rows through <c>RepoFactory</c>, so the repositories are built over an
///   in-memory cache and installed with <see cref="RepoFactoryScope"/>. Writes go through partial
///   mocks whose <c>Save</c> hands out local IDs the way the database would, because the link head
///   is "the smallest live ID" and half of what is asserted here depends on that being real.
///   Entities are Moq doubles resolved through a registered
///   <see cref="IAiringScheduleEntityResolver"/>, which keeps the tests clear of AniDB, TMDB and
///   shoko rows the service never looks at for its own logic.
/// </remarks>
[Collection(nameof(RepoFactoryCollection))]
public class AiringScheduleServiceTests
{
    #region Ownership

    [Fact]
    public void AddOrUpdateSchedule_RefusesAnUnregisteredProvider()
    {
        using var harness = new Harness();
        var stranger = new TestProvider("Stranger");

        Assert.Throws<ArgumentException>(() => harness.Service.AddOrUpdateSchedule(stranger, harness.ScheduleData()));
    }

    [Fact]
    public void AddOrUpdateSchedule_RefusesAKindTheProviderDoesNotDeclare()
    {
        using var harness = new Harness();

        Assert.Throws<ArgumentException>(() => harness.Service.AddOrUpdateSchedule(
            harness.Primary,
            harness.ScheduleData(tracks: [new AiringTrackData(AiringKind.Dubbed, "en")])
        ));
    }

    [Fact]
    public void AddOrUpdateSchedule_IsIdempotentOnItsIdentityAndUpdatesTheRest()
    {
        using var harness = new Harness();

        var first = harness.Service.AddOrUpdateSchedule(harness.Primary, harness.ScheduleData(key: "run"));
        var second = harness.Service.AddOrUpdateSchedule(harness.Primary, harness.ScheduleData(key: "run", url: "https://example.test/run"));

        Assert.Equal(first.ID, second.ID);
        Assert.Equal("https://example.test/run", second.Url);
        Assert.Single(harness.Schedules.Object.GetAll());
    }

    [Fact]
    public void AddOrUpdateSchedule_RefusesAChannelChangeForTheSameIdentity()
    {
        using var harness = new Harness();
        var other = harness.Service.FindOrRegisterChannel("BS11", AiringChannelType.Television);
        harness.Service.AddOrUpdateSchedule(harness.Primary, harness.ScheduleData(key: "run"));

        Assert.Throws<ArgumentException>(() => harness.Service.AddOrUpdateSchedule(
            harness.Primary,
            harness.ScheduleData(key: "run", channelID: other.ID)
        ));
    }

    [Fact]
    public void SetAirings_RefusesAnotherProvidersSchedule()
    {
        using var harness = new Harness();
        var schedule = harness.Service.AddOrUpdateSchedule(harness.Primary, harness.ScheduleData());

        Assert.Throws<ArgumentException>(() => harness.Service.SetAirings(harness.Secondary, schedule, []));
    }

    [Fact]
    public void SetAirings_RefusesAnEpisodeOutsideTheSchedulesCoverage()
    {
        using var harness = new Harness();
        var schedule = harness.Service.AddOrUpdateSchedule(harness.Primary, harness.ScheduleData(lastEpisodeNumber: 2));

        var exception = Assert.Throws<AiringScheduleValidationException>(() => harness.Service.SetAirings(
            harness.Primary,
            schedule,
            [new EpisodeAiringData() { Episode = harness.Episodes[2], AiredAt = harness.Air(3) }]
        ));
        Assert.Single(exception.ValidationErrors);
    }

    #endregion

    #region Delta Writes

    [Fact]
    public void MergeAirings_AddsWithoutDisturbingTheAiringsItDoesNotName()
    {
        using var harness = new Harness();
        var schedule = harness.Service.AddOrUpdateSchedule(harness.Primary, harness.ScheduleData());
        var before = harness.Service.SetAirings(harness.Primary, schedule, [
            new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = harness.Air(31) },
            new EpisodeAiringData() { Episode = harness.Episodes[1], AiredAt = harness.Air(38) },
        ]);

        var written = harness.Service.MergeAirings(harness.Primary, schedule, [
            new EpisodeAiringData() { Episode = harness.Episodes[2], AiredAt = harness.Air(45) },
        ]);

        // Only the airing the delta named came back, and the two it said nothing
        // about are still there, on their own slots and their own timestamps.
        var added = Assert.Single(written);
        Assert.Equal(harness.Air(45), added.AiredAt);
        var after = harness.Service.GetAiringsForSchedule(schedule.ID, new EpisodeAiringFilteringOptions() { IncludeEstimates = false });
        Assert.Equal(3, after.Count);
        foreach (var airing in before)
        {
            var kept = Assert.Single(after, entry => entry.ID == airing.ID);
            Assert.Equal(airing.AiredAt, kept.AiredAt);
            Assert.Equal(airing.LastUpdatedAt, kept.LastUpdatedAt);
        }
    }

    [Fact]
    public void MergeAirings_DeletesAnExplicitRemovalWithAFutureSlot()
    {
        using var harness = new Harness();
        var schedule = harness.Service.AddOrUpdateSchedule(harness.Primary, harness.ScheduleData());
        var airings = harness.Service.SetAirings(harness.Primary, schedule, [
            new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = harness.Air(31) },
            new EpisodeAiringData() { Episode = harness.Episodes[1], AiredAt = harness.Air(38) },
            new EpisodeAiringData() { Episode = harness.Episodes[2], AiredAt = harness.Air(45) },
        ]);
        var pulled = airings.Single(airing => airing.AiredAt == harness.Air(45));

        harness.Service.MergeAirings(harness.Primary, schedule, [], [pulled]);

        // Naming an airing means "this row was a mistake", so the slot still
        // ahead of us goes rather than being kept as a hiatus.
        var after = harness.Service.GetAiringsForSchedule(schedule.ID, new EpisodeAiringFilteringOptions() { IncludeEstimates = false });
        Assert.Equal(2, after.Count);
        Assert.DoesNotContain(after, entry => entry.ID == pulled.ID);
    }

    [Fact]
    public void MergeAirings_KeepsAnExplicitRemovalWithAFutureSlotAsAHiatusWhenAskedTo()
    {
        using var harness = new Harness();
        var schedule = harness.Service.AddOrUpdateSchedule(harness.Primary, harness.ScheduleData());
        var airings = harness.Service.SetAirings(harness.Primary, schedule, [
            new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = harness.Air(31) },
            new EpisodeAiringData() { Episode = harness.Episodes[1], AiredAt = harness.Air(38) },
            new EpisodeAiringData() { Episode = harness.Episodes[2], AiredAt = harness.Air(45) },
        ]);
        var pulled = airings.Single(airing => airing.AiredAt == harness.Air(45));

        harness.Service.MergeAirings(harness.Primary, schedule, [], [pulled], new EpisodeAiringUpdateOptions() { KeepRemovalsAsHiatus = true });

        // A provider whose removal means "my source pre-empted this" asks for
        // the judgement an omission gets, and a slot still ahead of us is then
        // kept without one.
        var after = harness.Service.GetAiringsForSchedule(schedule.ID, new EpisodeAiringFilteringOptions() { IncludeEstimates = false });
        Assert.Equal(3, after.Count);
        var hiatus = Assert.Single(after, entry => entry.ID == pulled.ID);
        Assert.Null(hiatus.AiredAt);
        Assert.Equal(harness.Air(45), hiatus.OriginalAiredAt);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MergeAirings_DeletesAnExplicitRemovalWhoseSlotHasPassed(bool keepRemovalsAsHiatus)
    {
        using var harness = new Harness();
        var schedule = harness.Service.AddOrUpdateSchedule(harness.Primary, harness.ScheduleData());
        var airings = harness.Service.SetAirings(harness.Primary, schedule, [
            new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = harness.Air(1) },
            new EpisodeAiringData() { Episode = harness.Episodes[1], AiredAt = harness.Air(8) },
            new EpisodeAiringData() { Episode = harness.Episodes[2], AiredAt = harness.Air(15) },
        ]);
        var pulled = airings.Single(airing => airing.AiredAt == harness.Air(15));

        harness.Service.MergeAirings(harness.Primary, schedule, [], [pulled], new EpisodeAiringUpdateOptions()
        {
            KeepRemovalsAsHiatus = keepRemovalsAsHiatus,
        });

        // The same removal a fortnight the other side of now is history either
        // way: there is no slot ahead of us left to hold open.
        var after = harness.Service.GetAiringsForSchedule(schedule.ID, new EpisodeAiringFilteringOptions() { IncludeEstimates = false });
        Assert.Equal(2, after.Count);
        Assert.DoesNotContain(after, entry => entry.ID == pulled.ID);
    }

    [Fact]
    public void MergeAirings_JudgesARemovalByTheCoverageItStatesWithoutWritingIt()
    {
        using var harness = new Harness();
        var schedule = harness.Service.AddOrUpdateSchedule(harness.Primary, harness.ScheduleData(lastEpisodeNumber: 4));
        var airings = harness.Service.SetAirings(harness.Primary, schedule, [
            new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = harness.Air(31) },
            new EpisodeAiringData() { Episode = harness.Episodes[2], AiredAt = harness.Air(45) },
        ]);
        var pulled = airings.Single(airing => airing.AiredAt == harness.Air(45));

        harness.Service.MergeAirings(harness.Primary, schedule, [], [pulled], new EpisodeAiringUpdateOptions()
        {
            KeepRemovalsAsHiatus = true,
            LastEpisodeNumber = 1,
        });

        // Stated coverage decided the removal — episode 3 is outside it, so the
        // future slot is history rather than a hiatus — and the schedule still
        // covers what it always did.
        var after = harness.Service.GetAiringsForSchedule(schedule.ID, new EpisodeAiringFilteringOptions() { IncludeEstimates = false });
        Assert.DoesNotContain(after, entry => entry.ID == pulled.ID);
        var reread = harness.Service.GetScheduleByID(schedule.ID);
        Assert.NotNull(reread);
        Assert.Equal(4, reread.LastEpisodeNumber);
        Assert.False(reread.IsFinished);
    }

    [Fact]
    public void MergeAirings_LeavesTheSchedulesCoverageAloneWhenItStatesNothing()
    {
        using var harness = new Harness();
        var schedule = harness.Service.AddOrUpdateSchedule(harness.Primary, harness.ScheduleData(lastEpisodeNumber: 4));
        harness.Service.SetAirings(harness.Primary, schedule, [
            new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = harness.Air(31) },
        ]);

        harness.Service.MergeAirings(harness.Primary, schedule, [
            new EpisodeAiringData() { Episode = harness.Episodes[1], AiredAt = harness.Air(38) },
        ]);

        var reread = harness.Service.GetScheduleByID(schedule.ID);
        Assert.NotNull(reread);
        Assert.Equal(4, reread.LastEpisodeNumber);
        Assert.Null(reread.FirstEpisodeNumber);
        Assert.False(reread.IsFinished);
    }

    [Fact]
    public void MergeAirings_RefusesARemovalFromAnotherSchedule()
    {
        using var harness = new Harness();
        var first = harness.Service.AddOrUpdateSchedule(harness.Primary, harness.ScheduleData(key: "one"));
        var second = harness.Service.AddOrUpdateSchedule(harness.Primary, harness.ScheduleData(key: "two"));
        var stranger = harness.Service.SetAirings(harness.Primary, second, [
            new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = harness.Air(31) },
        ]).Single();

        Assert.Throws<ArgumentException>(() => harness.Service.MergeAirings(harness.Primary, first, [], [stranger]));
    }

    [Fact]
    public void MergeAirings_RefusesAnAiringItIsAlsoAskedToRemove()
    {
        using var harness = new Harness();
        var schedule = harness.Service.AddOrUpdateSchedule(harness.Primary, harness.ScheduleData());
        var airing = harness.Service.SetAirings(harness.Primary, schedule, [
            new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = harness.Air(31) },
        ]).Single();

        var exception = Assert.Throws<AiringScheduleValidationException>(() => harness.Service.MergeAirings(
            harness.Primary,
            schedule,
            [new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = harness.Air(38) }],
            [airing]
        ));
        Assert.Single(exception.ValidationErrors);
    }

    [Fact]
    public void SetAirings_StillDeletesTheAiringsItWasNotGiven()
    {
        using var harness = new Harness();
        var schedule = harness.Service.AddOrUpdateSchedule(harness.Primary, harness.ScheduleData());
        harness.Service.SetAirings(harness.Primary, schedule, [
            new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = harness.Air(1) },
            new EpisodeAiringData() { Episode = harness.Episodes[1], AiredAt = harness.Air(8) },
        ]);

        harness.Service.SetAirings(harness.Primary, schedule, [
            new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = harness.Air(1) },
        ]);

        // Silence still means removal on the whole-line write, whatever the delta
        // one does with it.
        var after = harness.Service.GetAiringsForSchedule(schedule.ID, new EpisodeAiringFilteringOptions() { IncludeEstimates = false });
        var kept = Assert.Single(after);
        Assert.Equal(harness.Air(1), kept.AiredAt);
    }

    [Fact]
    public void SetAirings_StillReadsAnOmittedFutureSlotAsAHiatus()
    {
        using var harness = new Harness();
        var schedule = harness.Service.AddOrUpdateSchedule(harness.Primary, harness.ScheduleData());
        harness.Service.SetAirings(harness.Primary, schedule, [
            new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = harness.Air(31) },
            new EpisodeAiringData() { Episode = harness.Episodes[1], AiredAt = harness.Air(38) },
        ]);

        harness.Service.SetAirings(harness.Primary, schedule, [
            new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = harness.Air(31) },
        ]);

        // Absence really is the source no longer listing it, so the inference
        // the option took away from a named removal is untouched here.
        var after = harness.Service.GetAiringsForSchedule(schedule.ID, new EpisodeAiringFilteringOptions() { IncludeEstimates = false });
        Assert.Equal(2, after.Count);
        var hiatus = Assert.Single(after, entry => entry.EpisodeID == harness.Episodes[1].ID.ToString());
        Assert.Null(hiatus.AiredAt);
        Assert.Equal(harness.Air(38), hiatus.OriginalAiredAt);
    }

    #endregion

    #region Write Events

    [Fact]
    public void SetAirings_SplitsTheEventIntoWhatItAddedUpdatedAndWithdrew()
    {
        using var harness = new Harness();
        var schedule = harness.Service.AddOrUpdateSchedule(harness.Primary, harness.ScheduleData());
        var before = harness.Service.SetAirings(harness.Primary, schedule, [
            new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = harness.Air(38) },
            new EpisodeAiringData() { Episode = harness.Episodes[1], AiredAt = harness.Air(45) },
            new EpisodeAiringData() { Episode = harness.Episodes[2], AiredAt = harness.Air(52) },
        ]);
        var untouched = before.Single(airing => airing.EpisodeID == harness.Episodes[0].ID.ToString());
        // A view reads the row it stands on, so the timestamp is taken now.
        var untouchedAt = untouched.LastUpdatedAt;
        var events = new List<EpisodeAiringsUpdatedEventArgs>();
        harness.Service.AiringsUpdated += (_, e) => events.Add(e);

        harness.Service.SetAirings(harness.Primary, schedule, [
            // Exactly where it stands.
            new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = harness.Air(38) },
            // A day later than it stood.
            new EpisodeAiringData() { Episode = harness.Episodes[1], AiredAt = harness.Air(46) },
            // The third episode is left out of the line, and the fourth is new to it.
            new EpisodeAiringData() { Episode = harness.Episodes[3], AiredAt = harness.Air(59) },
        ]);

        // One event for the whole write, with the three things it did told apart.
        var raised = Assert.Single(events);
        Assert.Equal(UpdateReason.Updated, raised.Reason);
        Assert.Equal(harness.Episodes[3].ID.ToString(), Assert.Single(raised.Added).EpisodeID);
        Assert.Equal(harness.Episodes[1].ID.ToString(), Assert.Single(raised.Updated).EpisodeID);
        Assert.Equal(harness.Episodes[2].ID.ToString(), Assert.Single(raised.Withdrawn).EpisodeID);

        // The one the write handed back as it stands is in none of the three,
        // and its timestamp never moved.
        Assert.DoesNotContain(raised.Airings, airing => airing.EpisodeID == untouched.EpisodeID);
        var after = harness.Service.GetAiringsForSchedule(schedule.ID, new EpisodeAiringFilteringOptions() { IncludeEstimates = false });
        var kept = Assert.Single(after, entry => entry.EpisodeID == untouched.EpisodeID);
        Assert.Equal(untouchedAt, kept.LastUpdatedAt);
    }

    [Fact]
    public void SetAirings_RewritesNothingWhenTheWholeLineIsUnchanged()
    {
        using var harness = new Harness();
        var schedule = harness.Service.AddOrUpdateSchedule(harness.Primary, harness.ScheduleData());
        var line = new List<EpisodeAiringData>()
        {
            new() { Episode = harness.Episodes[0], AiredAt = harness.Air(38) },
            new() { Episode = harness.Episodes[1], AiredAt = harness.Air(45) },
        };
        var before = harness.Service.SetAirings(harness.Primary, schedule, line)
            .ToDictionary(airing => airing.EpisodeID, airing => airing.LastUpdatedAt);
        var events = new List<EpisodeAiringsUpdatedEventArgs>();
        harness.Service.AiringsUpdated += (_, e) => events.Add(e);

        var written = harness.Service.SetAirings(harness.Primary, schedule, line);

        // The same line again writes nothing and reports nothing, but the write
        // is still announced, so a consumer counting writes sees it happen.
        Assert.Empty(written);
        var raised = Assert.Single(events);
        Assert.Equal(UpdateReason.None, raised.Reason);
        Assert.Empty(raised.Added);
        Assert.Empty(raised.Updated);
        Assert.Empty(raised.Withdrawn);
        Assert.Empty(raised.Airings);

        // And no timestamp moved with it.
        var after = harness.Service.GetAiringsForSchedule(schedule.ID, new EpisodeAiringFilteringOptions() { IncludeEstimates = false });
        Assert.Equal(before, after.ToDictionary(airing => airing.EpisodeID, airing => airing.LastUpdatedAt));
    }

    [Fact]
    public void SetAirings_CountsAUrlTheProviderChangedAsAnUpdate()
    {
        using var harness = new Harness();
        var schedule = harness.Service.AddOrUpdateSchedule(harness.Primary, harness.ScheduleData());
        harness.Service.SetAirings(harness.Primary, schedule, [
            new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = harness.Air(38), Url = "https://example.test/one" },
        ]);
        var events = new List<EpisodeAiringsUpdatedEventArgs>();
        harness.Service.AiringsUpdated += (_, e) => events.Add(e);

        harness.Service.SetAirings(harness.Primary, schedule, [
            new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = harness.Air(38), Url = "https://example.test/two" },
        ]);

        // The slot didn't move, so the skip has to weigh more of the row than
        // the inference itself carries.
        var raised = Assert.Single(events);
        Assert.Equal(UpdateReason.Updated, raised.Reason);
        Assert.Equal("https://example.test/two", Assert.Single(raised.Updated).Url);
    }

    [Fact]
    public void SetAirings_ReportsAKeptHiatusAndADeletedHistoryAlikeAsWithdrawn()
    {
        using var harness = new Harness();
        var schedule = harness.Service.AddOrUpdateSchedule(harness.Primary, harness.ScheduleData());
        harness.Service.SetAirings(harness.Primary, schedule, [
            new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = harness.Air(1) },
            new EpisodeAiringData() { Episode = harness.Episodes[1], AiredAt = harness.Air(45) },
        ]);
        var events = new List<EpisodeAiringsUpdatedEventArgs>();
        harness.Service.AiringsUpdated += (_, e) => events.Add(e);

        harness.Service.SetAirings(harness.Primary, schedule, [
            new EpisodeAiringData() { Episode = harness.Episodes[2], AiredAt = harness.Air(52) },
        ]);

        var raised = Assert.Single(events);
        Assert.Equal(UpdateReason.Updated, raised.Reason);
        Assert.Equal(harness.Episodes[2].ID.ToString(), Assert.Single(raised.Added).EpisodeID);
        Assert.Empty(raised.Updated);
        Assert.Equal(2, raised.Withdrawn.Count);

        // A slot still ahead of us is kept without one, so it is off the line
        // rather than gone, and it is reported under the same heading as ...
        var after = harness.Service.GetAiringsForSchedule(schedule.ID, new EpisodeAiringFilteringOptions() { IncludeEstimates = false });
        var hiatus = Assert.Single(raised.Withdrawn, airing => airing.EpisodeID == harness.Episodes[1].ID.ToString());
        Assert.Null(hiatus.AiredAt);
        Assert.Equal(harness.Air(45), hiatus.OriginalAiredAt);
        Assert.Contains(after, entry => entry.ID == hiatus.ID);

        // ... the one whose slot has passed, which is history, and history goes.
        var history = Assert.Single(raised.Withdrawn, airing => airing.EpisodeID == harness.Episodes[0].ID.ToString());
        Assert.DoesNotContain(after, entry => entry.ID == history.ID);
    }

    [Fact]
    public void MergeAirings_RaisesAnEventAWholeLineWriteCannotBeToldFrom()
    {
        using var harness = new Harness();
        var whole = harness.Service.AddOrUpdateSchedule(harness.Primary, harness.ScheduleData(key: "whole"));
        var delta = harness.Service.AddOrUpdateSchedule(harness.Primary, harness.ScheduleData(key: "delta"));
        foreach (var schedule in new[] { whole, delta })
            harness.Service.SetAirings(harness.Primary, schedule, [
                new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = harness.Air(38) },
                new EpisodeAiringData() { Episode = harness.Episodes[1], AiredAt = harness.Air(45) },
                new EpisodeAiringData() { Episode = harness.Episodes[2], AiredAt = harness.Air(52) },
            ]);
        var pulled = harness.Service.GetAiringsForSchedule(delta.ID, new EpisodeAiringFilteringOptions() { IncludeEstimates = false })
            .Single(airing => airing.EpisodeID == harness.Episodes[2].ID.ToString());
        var events = new List<EpisodeAiringsUpdatedEventArgs>();
        harness.Service.AiringsUpdated += (_, e) => events.Add(e);

        // The same change stated the two ways it can be: the whole line, with
        // the dropped entry left out of it, against only the parts that moved.
        // The delta asks for the hiatus judgement, since that is what leaving an
        // airing out of a whole-line write means.
        harness.Service.SetAirings(harness.Primary, whole, [
            new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = harness.Air(38) },
            new EpisodeAiringData() { Episode = harness.Episodes[1], AiredAt = harness.Air(46) },
            new EpisodeAiringData() { Episode = harness.Episodes[3], AiredAt = harness.Air(59) },
        ]);
        harness.Service.MergeAirings(harness.Primary, delta, [
            new EpisodeAiringData() { Episode = harness.Episodes[1], AiredAt = harness.Air(46) },
            new EpisodeAiringData() { Episode = harness.Episodes[3], AiredAt = harness.Air(59) },
        ], [pulled], new EpisodeAiringUpdateOptions() { KeepRemovalsAsHiatus = true });

        // Which entry point a provider reached for is not something a consumer
        // can read off the event.
        Assert.Equal(2, events.Count);
        Assert.Equal(events[0].Reason, events[1].Reason);
        Assert.Equal(Describe(events[0].Added), Describe(events[1].Added));
        Assert.Equal(Describe(events[0].Updated), Describe(events[1].Updated));
        Assert.Equal(Describe(events[0].Withdrawn), Describe(events[1].Withdrawn));

        static List<(string EpisodeID, DateTime? AiredAt, DateTime? OriginalAiredAt, bool IsDelayed)> Describe(IReadOnlyList<IEpisodeAiring> airings)
            => airings.Select(airing => (airing.EpisodeID, airing.AiredAt, airing.OriginalAiredAt, airing.IsDelayed)).ToList();
    }

    #endregion

    #region Selection & Preference

    [Fact]
    public void GetAiringForEpisode_PrefersTheTrackThenTheChannel()
    {
        using var harness = new Harness();
        var tokyo = harness.Service.FindOrRegisterChannel("TOKYO MX", AiringChannelType.Television);
        var crunchyroll = harness.Service.FindOrRegisterChannel("Crunchyroll", AiringChannelType.Streaming);
        harness.Schedule(harness.Primary, "mx", tokyo.ID, [new AiringTrackData(AiringKind.Original, "ja")], (0, harness.Air(1, 14)));
        harness.Schedule(harness.Primary, "cr", crunchyroll.ID, [new AiringTrackData(AiringKind.Subtitled, "en")], (0, harness.Air(1, 15)));

        var subtitled = harness.Service.GetAiringForEpisode(harness.Episodes[0], new EpisodeAiringFilteringOptions()
        {
            PreferredTracks = [new AiringTrackPreference(AiringKind.Subtitled, "en")],
            PreferredChannels = [tokyo.ID],
        });

        // The track preference outranks the channel preference, so the later
        // subtitled release wins over the preferred station's broadcast.
        Assert.NotNull(subtitled);
        Assert.Equal(crunchyroll.ID, subtitled.Channel?.ID);
    }

    [Fact]
    public void GetAiringForEpisode_WithoutAPreferenceTakesTheEarliestAiring()
    {
        using var harness = new Harness();
        var tokyo = harness.Service.FindOrRegisterChannel("TOKYO MX", AiringChannelType.Television);
        var bs11 = harness.Service.FindOrRegisterChannel("BS11", AiringChannelType.Television);
        harness.Schedule(harness.Primary, "bs11", bs11.ID, [new AiringTrackData(AiringKind.Original, "ja")], (0, harness.Air(1, 23, 30)));
        harness.Schedule(harness.Primary, "mx", tokyo.ID, [new AiringTrackData(AiringKind.Original, "ja")], (0, harness.Air(1, 23)));

        var earliest = harness.Service.GetAiringForEpisode(harness.Episodes[0], new EpisodeAiringFilteringOptions()
        {
            PreferredChannels = [],
            PreferredTracks = [],
        });

        Assert.NotNull(earliest);
        Assert.Equal(tokyo.ID, earliest.Channel?.ID);
    }

    [Fact]
    public void GetAiringsForEpisode_FiltersByChannel()
    {
        using var harness = new Harness();
        var tokyo = harness.Service.FindOrRegisterChannel("TOKYO MX", AiringChannelType.Television);
        var bs11 = harness.Service.FindOrRegisterChannel("BS11", AiringChannelType.Television);
        harness.Schedule(harness.Primary, "mx", tokyo.ID, [new AiringTrackData(AiringKind.Original, "ja")], (0, harness.Air(1, 23)));
        harness.Schedule(harness.Primary, "bs11", bs11.ID, [new AiringTrackData(AiringKind.Original, "ja")], (0, harness.Air(1, 23, 30)));

        var airings = harness.Service.GetAiringsForEpisode(harness.Episodes[0], new EpisodeAiringFilteringOptions()
        {
            ChannelIDs = new HashSet<Guid> { bs11.ID },
            IncludeEstimates = false,
        });

        var airing = Assert.Single(airings);
        Assert.Equal(bs11.ID, airing.Channel?.ID);
    }

    [Fact]
    public void GetAiringsForEpisode_KeepsTheHigherPriorityProvidersAiringForOneChannel()
    {
        using var harness = new Harness();
        var crunchyroll = harness.Service.FindOrRegisterChannel("Crunchyroll", AiringChannelType.Streaming);
        var tracks = new[] { new AiringTrackData(AiringKind.Subtitled, "en") };
        harness.Schedule(harness.Primary, "first", crunchyroll.ID, tracks, (0, harness.Air(1, 15)));
        harness.Schedule(harness.Secondary, "second", crunchyroll.ID, tracks, (0, harness.Air(1, 16)));

        var airings = harness.Service.GetAiringsForEpisode(harness.Episodes[0], new EpisodeAiringFilteringOptions() { IncludeEstimates = false });

        var airing = Assert.Single(airings);
        Assert.Equal(harness.Service.GetProviderInfo(harness.Primary).ID, airing.ProviderID);
    }

    [Fact]
    public void GetAiringsForEpisode_NeverDeduplicatesChannellessAirings()
    {
        using var harness = new Harness();
        var tracks = new[] { new AiringTrackData(AiringKind.Subtitled, "en") };
        harness.Schedule(harness.Primary, "first", null, tracks, (0, harness.Air(1, 15)));
        harness.Schedule(harness.Secondary, "second", null, tracks, (0, harness.Air(1, 16)));

        var airings = harness.Service.GetAiringsForEpisode(harness.Episodes[0], new EpisodeAiringFilteringOptions() { IncludeEstimates = false });

        Assert.Equal(2, airings.Count);
    }

    [Fact]
    public void GetAiringsForEpisode_EstimatesTheEpisodesTheScheduleHasNoAiringFor()
    {
        using var harness = new Harness();
        var tokyo = harness.Service.FindOrRegisterChannel("TOKYO MX", AiringChannelType.Television);
        harness.Schedule(
            harness.Primary,
            "mx",
            tokyo.ID,
            [new AiringTrackData(AiringKind.Original, "ja")],
            (0, harness.Air(1, 15, 30)),
            (1, harness.Air(8, 15, 30))
        );

        var estimated = Assert.Single(harness.Service.GetAiringsForEpisode(harness.Episodes[2]));
        Assert.True(estimated.IsEstimated);
        Assert.Equal(harness.Air(15, 15, 30), estimated.AiredAt);

        Assert.Empty(harness.Service.GetAiringsForEpisode(harness.Episodes[2], new EpisodeAiringFilteringOptions() { IncludeEstimates = false }));
    }

    [Fact]
    public void GetAiringsForEpisode_PreferredOnlyGivesOneAiringPerEpisode()
    {
        using var harness = new Harness();
        var tokyo = harness.Service.FindOrRegisterChannel("TOKYO MX", AiringChannelType.Television);
        var bs11 = harness.Service.FindOrRegisterChannel("BS11", AiringChannelType.Television);
        harness.Schedule(harness.Primary, "mx", tokyo.ID, [new AiringTrackData(AiringKind.Original, "ja")], (0, harness.Air(1, 23)));
        harness.Schedule(harness.Primary, "bs11", bs11.ID, [new AiringTrackData(AiringKind.Original, "ja")], (0, harness.Air(1, 23, 30)));

        Assert.Single(harness.Service.GetAiringsForEpisode(harness.Episodes[0], new EpisodeAiringFilteringOptions() { PreferredOnly = true }));
    }

    [Fact]
    public void GetAiringsForEpisode_CollapsesOneChannelTwoProvidersSpellTheLanguageOfDifferently()
    {
        using var harness = new Harness();
        var crunchyroll = harness.Service.FindOrRegisterChannel("Crunchyroll", AiringChannelType.Streaming);
        harness.Schedule(harness.Primary, "first", crunchyroll.ID, [new AiringTrackData(AiringKind.Subtitled, "en")], (0, harness.Air(1, 15)));
        harness.Schedule(harness.Secondary, "second", crunchyroll.ID, [new AiringTrackData(AiringKind.Subtitled, "eng")], (0, harness.Air(1, 15)));

        // "en" and "eng" are the same language, so the two lines are one slot.
        var airing = Assert.Single(harness.Service.GetAiringsForEpisode(harness.Episodes[0], new EpisodeAiringFilteringOptions() { IncludeEstimates = false }));
        Assert.Equal(harness.Service.GetProviderInfo(harness.Primary).ID, airing.ProviderID);
    }

    [Fact]
    public void GetAiringsForEpisode_KeepsARepeatBroadcastOnOneChannel()
    {
        using var harness = new Harness();
        var aichi = harness.Service.FindOrRegisterChannel("テレビ愛知", AiringChannelType.Television);
        harness.Repeats(aichi.ID, 0, harness.Air(1, 23), harness.Air(4, 2), harness.Air(6, 3));

        // Three slots the channel itself lists for the one episode, which is a
        // run of repeats rather than three providers reporting one broadcast.
        var airings = harness.Service.GetAiringsForEpisode(harness.Episodes[0], new EpisodeAiringFilteringOptions() { IncludeEstimates = false });

        Assert.Equal(3, airings.Count);
        Assert.Equal([harness.Air(1, 23), harness.Air(4, 2), harness.Air(6, 3)], airings.Select(airing => airing.AiredAt).ToList());
    }

    [Fact]
    public void GetAiringsForSchedule_KeepsEveryEpisodeOnTheSchedule()
    {
        using var harness = new Harness();
        var tokyo = harness.Service.FindOrRegisterChannel("TOKYO MX", AiringChannelType.Television);
        var schedule = harness.Schedule(
            harness.Primary,
            "mx",
            tokyo.ID,
            [new AiringTrackData(AiringKind.Original, "ja")],
            (0, harness.Air(1, 23)),
            (1, harness.Air(8, 23)),
            (2, harness.Air(15, 23))
        );

        // Every airing on a schedule shares its channel and its tracks, so a
        // read that de-duplicated on those alone answered with one episode.
        var airings = harness.Service.GetAiringsForSchedule(schedule.ID, new EpisodeAiringFilteringOptions() { IncludeEstimates = false });

        Assert.Equal(3, airings.Count);
        Assert.Equal(3, airings.Select(airing => airing.EpisodeID).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void GetAiringsInRange_FindsARepeatBroadcastOnItsOwnDay()
    {
        using var harness = new Harness();
        var aichi = harness.Service.FindOrRegisterChannel("テレビ愛知", AiringChannelType.Television);
        harness.Repeats(aichi.ID, 0, harness.Air(1, 23), harness.Air(4, 2), harness.Air(6, 3));

        var airings = harness.Service.GetAiringsInRange(
            harness.Air(4, 0),
            harness.Air(4, 23, 59),
            new EpisodeAiringFilteringOptions() { IncludeEstimates = false }
        );

        var airing = Assert.Single(airings);
        Assert.Equal(harness.Air(4, 2), airing.AiredAt);
    }

    [Fact]
    public void GetAiringsInRange_PreferredOnlyKeepsTheBestAiringInsideTheWindow()
    {
        using var harness = new Harness();
        var tbs = harness.Service.FindOrRegisterChannel("TBS", AiringChannelType.Television);
        var atx = harness.Service.FindOrRegisterChannel("AT-X", AiringChannelType.Television);
        harness.Schedule(harness.Primary, "tbs", tbs.ID, [new AiringTrackData(AiringKind.Original, "ja")], (0, harness.Air(1, 23)));
        harness.Schedule(harness.Primary, "atx", atx.ID, [new AiringTrackData(AiringKind.Original, "ja")], (0, harness.Air(3, 23)));

        // The episode's best airing overall is the earlier one on TBS, which is
        // outside this window; the day it actually airs on AT-X still has it.
        var airings = harness.Service.GetAiringsInRange(
            harness.Air(3, 0),
            harness.Air(3, 23, 59),
            new EpisodeAiringFilteringOptions() { IncludeEstimates = false, PreferredOnly = true }
        );

        var airing = Assert.Single(airings);
        Assert.Equal(atx.ID, airing.Channel?.ID);
    }

    [Fact]
    public void GetAiringsInRange_PreferredOnlyStillGivesOneAiringPerEpisode()
    {
        using var harness = new Harness();
        var tbs = harness.Service.FindOrRegisterChannel("TBS", AiringChannelType.Television);
        var atx = harness.Service.FindOrRegisterChannel("AT-X", AiringChannelType.Television);
        harness.Schedule(harness.Primary, "tbs", tbs.ID, [new AiringTrackData(AiringKind.Original, "ja")], (0, harness.Air(3, 22)), (1, harness.Air(3, 20)));
        harness.Schedule(harness.Primary, "atx", atx.ID, [new AiringTrackData(AiringKind.Original, "ja")], (0, harness.Air(3, 23)), (1, harness.Air(3, 21)));

        var airings = harness.Service.GetAiringsInRange(
            harness.Air(3, 0),
            harness.Air(3, 23, 59),
            new EpisodeAiringFilteringOptions() { IncludeEstimates = false, PreferredOnly = true }
        );

        // Two episodes air in the window, so the reduced read answers with two.
        Assert.Equal(2, airings.Count);
        Assert.Equal(2, airings.Select(airing => airing.EpisodeID).Distinct(StringComparer.Ordinal).Count());
        Assert.All(airings, airing => Assert.Equal(tbs.ID, airing.Channel?.ID));
    }

    [Fact]
    public void GetAiringByID_ResolvesAnEstimateTheSameWayItResolvesAStoredAiring()
    {
        using var harness = new Harness();
        var tokyo = harness.Service.FindOrRegisterChannel("TOKYO MX", AiringChannelType.Television);
        harness.Schedule(
            harness.Primary,
            "mx",
            tokyo.ID,
            [new AiringTrackData(AiringKind.Original, "ja")],
            (0, harness.Air(1, 15, 30)),
            (1, harness.Air(8, 15, 30))
        );

        var estimated = Assert.Single(harness.Service.GetAiringsForEpisode(harness.Episodes[2]));
        Assert.True(estimated.IsEstimated);

        var resolved = harness.Service.GetAiringByID(estimated.ID);

        Assert.NotNull(resolved);
        Assert.True(resolved.IsEstimated);
        Assert.Equal(estimated.ID, resolved.ID);
        Assert.Equal(estimated.AiredAt, resolved.AiredAt);
        Assert.Empty(harness.Service.GetLinkedAirings(estimated.ID));
    }

    [Fact]
    public void GetAiringByID_StillAnswersNothingForAnIDNothingProduces()
    {
        using var harness = new Harness();
        var tokyo = harness.Service.FindOrRegisterChannel("TOKYO MX", AiringChannelType.Television);
        harness.Schedule(harness.Primary, "mx", tokyo.ID, [new AiringTrackData(AiringKind.Original, "ja")], (0, harness.Air(1, 15, 30)));

        Assert.Null(harness.Service.GetAiringByID(Guid.NewGuid()));
    }

    #endregion

    #region Linked Entities

    [Fact]
    public void GetAiringsForEpisode_FollowsLinksForAShokoEpisodeByDefault()
    {
        using var harness = new Harness();
        var crunchyroll = harness.Service.FindOrRegisterChannel("Crunchyroll", AiringChannelType.Streaming);
        harness.Schedule(harness.Primary, "cr", crunchyroll.ID, [new AiringTrackData(AiringKind.Subtitled, "en")], (0, harness.Air(1, 15)));

        // The airing is stored against the provider's own episode, so only a
        // read that follows the shoko episode's links finds it.
        var linked = harness.Service.GetAiringsForEpisode(harness.ShokoEpisodes[0], new EpisodeAiringFilteringOptions() { IncludeEstimates = false });
        var own = harness.Service.GetAiringsForEpisode(
            harness.ShokoEpisodes[0],
            new EpisodeAiringFilteringOptions() { IncludeEstimates = false, LinkedEntityAirings = false }
        );

        Assert.Single(linked);
        Assert.Empty(own);
    }

    [Fact]
    public void GetAiringsForEpisode_DoesNotFollowLinksForAProviderEpisodeUnlessAsked()
    {
        using var harness = new Harness();
        var crunchyroll = harness.Service.FindOrRegisterChannel("Crunchyroll", AiringChannelType.Streaming);
        harness.Schedule(
            harness.Primary,
            "cr",
            crunchyroll.ID,
            [new AiringTrackData(AiringKind.Subtitled, "en")],
            shokoEntities: true,
            (0, harness.Air(1, 15))
        );

        // This time the airing sits on the shoko episode, which a provider
        // entity only reaches by being told to follow its links.
        Assert.Empty(harness.Service.GetAiringsForEpisode(harness.Episodes[0], new EpisodeAiringFilteringOptions() { IncludeEstimates = false }));
        Assert.Single(harness.Service.GetAiringsForEpisode(harness.Episodes[0], new EpisodeAiringFilteringOptions()
        {
            IncludeEstimates = false,
            LinkedEntityAirings = true,
        }));
    }

    #endregion

    #region Linking

    [Fact]
    public void LinkAirings_PointsEveryMemberAtTheSmallestLiveID()
    {
        using var harness = new Harness();
        var schedule = harness.Service.AddOrUpdateSchedule(harness.Primary, harness.ScheduleData());
        var airings = harness.Service.SetAirings(harness.Primary, schedule, [
            new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = harness.Air(1, 15) },
            new EpisodeAiringData() { Episode = harness.Episodes[1], AiredAt = harness.Air(1, 15) },
        ]);

        var linked = harness.Service.LinkAirings(harness.Primary, [airings[1], airings[0]]);

        Assert.Equal(2, linked.Count);
        Assert.All(linked, airing => Assert.Equal(linked[0].ID, airing.LinkID));
        Assert.Equal(linked[0].ID, linked[0].LinkID);
    }

    [Fact]
    public void LinkAirings_RefusesAiringsFromTwoSchedules()
    {
        using var harness = new Harness();
        var first = harness.Service.AddOrUpdateSchedule(harness.Primary, harness.ScheduleData(key: "one"));
        var second = harness.Service.AddOrUpdateSchedule(harness.Primary, harness.ScheduleData(key: "two"));
        var one = harness.Service.SetAirings(harness.Primary, first, [
            new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = harness.Air(1, 15) },
        ]).Single();
        var two = harness.Service.SetAirings(harness.Primary, second, [
            new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = harness.Air(1, 15) },
        ]).Single();

        Assert.Throws<ArgumentException>(() => harness.Service.LinkAirings(harness.Primary, [one, two]));
    }

    [Fact]
    public void MergeAirings_PromotesTheNextSmallestWhenTheHeadGoes()
    {
        using var harness = new Harness();
        var schedule = harness.Service.AddOrUpdateSchedule(harness.Primary, harness.ScheduleData());
        var airings = harness.Service.SetAirings(harness.Primary, schedule, [
            new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = harness.Air(1, 15) },
            new EpisodeAiringData() { Episode = harness.Episodes[1], AiredAt = harness.Air(1, 15) },
            new EpisodeAiringData() { Episode = harness.Episodes[2], AiredAt = harness.Air(1, 15) },
        ]);
        var linked = harness.Service.LinkAirings(harness.Primary, airings);

        harness.Service.MergeAirings(harness.Primary, schedule, [], [linked[0]]);

        var remaining = harness.Service.GetAiringsForSchedule(schedule.ID, new EpisodeAiringFilteringOptions() { IncludeEstimates = false });
        Assert.Equal(2, remaining.Count);
        var head = Assert.Single(remaining.Select(airing => airing.LinkID).Distinct());
        Assert.Contains(remaining, airing => airing.ID == head);
    }

    [Fact]
    public void UnlinkAiring_DissolvesASetOfTwo()
    {
        using var harness = new Harness();
        var schedule = harness.Service.AddOrUpdateSchedule(harness.Primary, harness.ScheduleData());
        var airings = harness.Service.SetAirings(harness.Primary, schedule, [
            new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = harness.Air(1, 15) },
            new EpisodeAiringData() { Episode = harness.Episodes[1], AiredAt = harness.Air(1, 15) },
        ]);
        var linked = harness.Service.LinkAirings(harness.Primary, airings);

        harness.Service.UnlinkAiring(harness.Primary, linked[1]);

        var remaining = harness.Service.GetAiringsForSchedule(schedule.ID, new EpisodeAiringFilteringOptions() { IncludeEstimates = false });
        Assert.All(remaining, airing => Assert.Null(airing.LinkID));
    }

    [Fact]
    public void LinkAirings_SurvivesAWriteThatKeepsTheMembersTogether()
    {
        using var harness = new Harness();
        var schedule = harness.Service.AddOrUpdateSchedule(harness.Primary, harness.ScheduleData());
        var airings = harness.Service.SetAirings(harness.Primary, schedule, [
            new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = harness.Air(1, 15) },
            new EpisodeAiringData() { Episode = harness.Episodes[1], AiredAt = harness.Air(1, 15) },
        ]);
        harness.Service.LinkAirings(harness.Primary, airings);

        var rewritten = harness.Service.SetAirings(harness.Primary, schedule, [
            new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = harness.Air(8, 15) },
            new EpisodeAiringData() { Episode = harness.Episodes[1], AiredAt = harness.Air(8, 15) },
        ]);

        Assert.All(rewritten, airing => Assert.NotNull(airing.LinkID));
        Assert.Single(rewritten.Select(airing => airing.LinkID).Distinct());
    }

    #endregion

    #region Retention

    [Fact]
    public void RetentionSweep_RemovesAScheduleWhoseRunEndedBeforeTheWindow()
    {
        using var harness = new Harness();
        harness.Settings.AutoCleanup = false;
        var schedule = harness.Service.AddOrUpdateSchedule(harness.Primary, harness.ScheduleData());
        harness.Service.SetAirings(harness.Primary, schedule, [
            new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = DateTime.UtcNow.AddYears(-3) },
            new EpisodeAiringData() { Episode = harness.Episodes[1], AiredAt = DateTime.UtcNow.AddYears(-2) },
        ]);
        harness.Settings.AutoCleanup = true;

        Assert.Equal(1, harness.Service.RunRetentionSweep());
        Assert.Empty(harness.Schedules.Object.GetAll());
        Assert.Empty(harness.Airings.Object.GetAll());
    }

    [Fact]
    public void RetentionSweep_KeepsAScheduleWhoseLastAiringIsInsideTheWindow()
    {
        using var harness = new Harness();
        harness.Settings.AutoCleanup = false;
        var schedule = harness.Service.AddOrUpdateSchedule(harness.Primary, harness.ScheduleData());
        harness.Service.SetAirings(harness.Primary, schedule, [
            new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = DateTime.UtcNow.AddYears(-5) },
            new EpisodeAiringData() { Episode = harness.Episodes[1], AiredAt = DateTime.UtcNow.AddDays(-1) },
        ]);
        harness.Settings.AutoCleanup = true;

        Assert.Equal(0, harness.Service.RunRetentionSweep());
        Assert.Equal(2, harness.Airings.Object.GetAll().Count);
    }

    [Fact]
    public void RetentionSweep_DoesNothingWhileAutoCleanupIsOff()
    {
        using var harness = new Harness();
        harness.Settings.AutoCleanup = false;
        var schedule = harness.Service.AddOrUpdateSchedule(harness.Primary, harness.ScheduleData());
        harness.Service.SetAirings(harness.Primary, schedule, [
            new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = DateTime.UtcNow.AddYears(-4) },
        ]);

        Assert.Equal(0, harness.Service.RunRetentionSweep());
        Assert.Single(harness.Schedules.Object.GetAll());
    }

    [Fact]
    public void RetentionSweep_LeavesAnEmptyScheduleAloneInsideItsBuffer()
    {
        using var harness = new Harness();
        harness.Service.AddOrUpdateSchedule(harness.Primary, harness.ScheduleData());

        Assert.Equal(0, harness.Service.RunRetentionSweep());
        Assert.Single(harness.Schedules.Object.GetAll());
    }

    [Fact]
    public void RetentionSweep_TakesAnEmptySchedulePastItsBuffer()
    {
        using var harness = new Harness();
        harness.Service.AddOrUpdateSchedule(harness.Primary, harness.ScheduleData());
        var row = Assert.Single(harness.Schedules.Object.GetAll());
        row.CreatedAt = DateTime.UtcNow.AddHours(-2);

        Assert.Equal(1, harness.Service.RunRetentionSweep());
        Assert.Empty(harness.Schedules.Object.GetAll());
    }

    [Fact]
    public void Settings_ClampTheRetentionWindowToTheMinimum()
    {
        using var harness = new Harness();
        harness.Settings.RetentionMonths = 1;

        Assert.Equal(AiringScheduleServiceSettings.MinimumRetentionMonths, harness.Service.LoadSettings().RetentionMonths);
    }

    [Fact]
    public void SetAirings_RefusesARunThatHasAgedOutWhole()
    {
        using var harness = new Harness();
        var schedule = harness.Service.AddOrUpdateSchedule(harness.Primary, harness.ScheduleData());

        var exception = Assert.Throws<AiringScheduleValidationException>(() => harness.Service.SetAirings(harness.Primary, schedule, [
            new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = DateTime.UtcNow.AddYears(-3) },
        ]));
        // The whole schedule is what would go, so the error is keyed on it
        // rather than on the airing that carries the slot.
        Assert.Equal("#schedule", Assert.Single(exception.ValidationErrors).Key);

        harness.Settings.AutoCleanup = false;
        Assert.Single(harness.Service.SetAirings(harness.Primary, schedule, [
            new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = DateTime.UtcNow.AddYears(-3) },
        ]));

        harness.Settings.AutoCleanup = true;
        // And what the write refuses is exactly what the sweep takes away.
        Assert.Equal(1, harness.Service.RunRetentionSweep());
    }

    [Fact]
    public void SetAirings_KeepsOldAiringsBesideOneInsideTheWindow()
    {
        using var harness = new Harness();
        var schedule = harness.Service.AddOrUpdateSchedule(harness.Primary, harness.ScheduleData());

        var written = harness.Service.SetAirings(harness.Primary, schedule, [
            new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = DateTime.UtcNow.AddYears(-8) },
            new EpisodeAiringData() { Episode = harness.Episodes[1], AiredAt = DateTime.UtcNow.AddYears(-7) },
            new EpisodeAiringData() { Episode = harness.Episodes[2], AiredAt = DateTime.UtcNow.AddDays(-1) },
        ]);

        Assert.Equal(3, written.Count);
        // A run that still airs keeps its whole history, and the sweep agrees.
        Assert.Equal(0, harness.Service.RunRetentionSweep());
        Assert.Equal(3, harness.Airings.Object.GetAll().Count);
    }

    [Fact]
    public void SetAirings_TakesTheSlotOnTheNearSideOfTheCutoff()
    {
        using var harness = new Harness();
        var schedule = harness.Service.AddOrUpdateSchedule(harness.Primary, harness.ScheduleData());
        // The service reads the clock itself, so the cutoff is approached from a
        // minute either side of it rather than landed on exactly.
        var months = harness.Service.LoadSettings().RetentionMonths;

        Assert.Single(harness.Service.SetAirings(harness.Primary, schedule, [
            new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = DateTime.UtcNow.AddMonths(-months).AddMinutes(1) },
        ]));
        Assert.Equal(0, harness.Service.RunRetentionSweep());

        var exception = Assert.Throws<AiringScheduleValidationException>(() => harness.Service.SetAirings(harness.Primary, schedule, [
            new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = DateTime.UtcNow.AddMonths(-months).AddMinutes(-1) },
        ]));
        Assert.Equal("#schedule", Assert.Single(exception.ValidationErrors).Key);
    }

    [Fact]
    public void SetAirings_TakesAScheduleOfSlotlessAirings()
    {
        using var harness = new Harness();
        var schedule = harness.Service.AddOrUpdateSchedule(harness.Primary, harness.ScheduleData());

        var written = harness.Service.SetAirings(harness.Primary, schedule, [
            new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = null },
            new EpisodeAiringData() { Episode = harness.Episodes[1], AiredAt = null },
        ]);

        // An airing with no slot has no date to judge, which is the arm the
        // sweep keeps the schedule under as well.
        Assert.Equal(2, written.Count);
        Assert.Equal(0, harness.Service.RunRetentionSweep());
        Assert.Equal(2, harness.Airings.Object.GetAll().Count);

        // One aged-out slot beside them still decides it.
        var exception = Assert.Throws<AiringScheduleValidationException>(() => harness.Service.SetAirings(harness.Primary, schedule, [
            new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = null },
            new EpisodeAiringData() { Episode = harness.Episodes[1], AiredAt = DateTime.UtcNow.AddYears(-3) },
        ]));
        Assert.Equal("#schedule", Assert.Single(exception.ValidationErrors).Key);
    }

    [Fact]
    public void SetAirings_CountsAHiatusTheWriteKeepsTowardsRetention()
    {
        using var harness = new Harness();
        var schedule = harness.Service.AddOrUpdateSchedule(harness.Primary, harness.ScheduleData());
        harness.Service.SetAirings(harness.Primary, schedule, [
            new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = DateTime.UtcNow.AddYears(-3) },
            new EpisodeAiringData() { Episode = harness.Episodes[1], AiredAt = DateTime.UtcNow.AddDays(7) },
        ]);

        // Leaving the slot ahead of us out keeps it as a hiatus, so it is still
        // on the schedule the sweep would find.
        harness.Service.SetAirings(harness.Primary, schedule, [
            new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = DateTime.UtcNow.AddYears(-3) },
        ]);
        Assert.Equal(2, harness.Airings.Object.GetAll().Count);
        Assert.Equal(0, harness.Service.RunRetentionSweep());

        // The same omission on a finished run is history rather than a hiatus,
        // which leaves nothing inside the window at all.
        var exception = Assert.Throws<AiringScheduleValidationException>(() => harness.Service.SetAirings(
            harness.Primary,
            schedule,
            [new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = DateTime.UtcNow.AddYears(-3) }],
            new EpisodeAiringUpdateOptions() { IsFinished = true }
        ));
        Assert.Equal("#schedule", Assert.Single(exception.ValidationErrors).Key);
    }

    [Fact]
    public void MergeAirings_CountsARemovalTowardsRetentionOnlyWhileItIsKept()
    {
        using var harness = new Harness();
        var schedule = harness.Service.AddOrUpdateSchedule(harness.Primary, harness.ScheduleData());
        var airings = harness.Service.SetAirings(harness.Primary, schedule, [
            new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = DateTime.UtcNow.AddYears(-3) },
            new EpisodeAiringData() { Episode = harness.Episodes[1], AiredAt = DateTime.UtcNow.AddDays(7) },
        ]);
        var pulled = airings.Single(airing => airing.EpisodeID == harness.Episodes[1].ID.ToString());

        // Deleting the only slot inside the window leaves nothing behind for
        // retention to judge on, so the write is refused outright.
        var exception = Assert.Throws<AiringScheduleValidationException>(
            () => harness.Service.MergeAirings(harness.Primary, schedule, [], [pulled])
        );
        Assert.Equal("#schedule", Assert.Single(exception.ValidationErrors).Key);

        // Keeping it as a hiatus does not: the row loses its slot but holds on
        // to the one it lost, and that is what retention judges it by.
        harness.Service.MergeAirings(harness.Primary, schedule, [], [pulled], new EpisodeAiringUpdateOptions() { KeepRemovalsAsHiatus = true });
        Assert.Equal(2, harness.Airings.Object.GetAll().Count);
        Assert.Equal(0, harness.Service.RunRetentionSweep());
    }

    [Fact]
    public void MergeAirings_JudgesRetentionOnWhatTheDeltaLeavesBehind()
    {
        using var harness = new Harness();
        var schedule = harness.Service.AddOrUpdateSchedule(harness.Primary, harness.ScheduleData());
        harness.Service.SetAirings(harness.Primary, schedule, [
            new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = DateTime.UtcNow.AddDays(-1) },
        ]);

        // The delta on its own has aged out; the airing it leaves alone has not.
        harness.Service.MergeAirings(harness.Primary, schedule, [
            new EpisodeAiringData() { Episode = harness.Episodes[1], AiredAt = DateTime.UtcNow.AddYears(-3) },
        ]);

        Assert.Equal(2, harness.Airings.Object.GetAll().Count);
        Assert.Equal(0, harness.Service.RunRetentionSweep());
    }

    [Fact]
    public void MergeAirings_RefusesARemovalThatTakesTheLastAiringInsideTheWindow()
    {
        using var harness = new Harness();
        var schedule = harness.Service.AddOrUpdateSchedule(harness.Primary, harness.ScheduleData());
        var airings = harness.Service.SetAirings(harness.Primary, schedule, [
            new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = DateTime.UtcNow.AddYears(-3) },
            new EpisodeAiringData() { Episode = harness.Episodes[1], AiredAt = DateTime.UtcNow.AddDays(-1) },
        ]);
        var recent = airings.Single(airing => airing.EpisodeID == harness.Episodes[1].ID.ToString());

        var exception = Assert.Throws<AiringScheduleValidationException>(
            () => harness.Service.MergeAirings(harness.Primary, schedule, [], [recent])
        );

        Assert.Equal("#schedule", Assert.Single(exception.ValidationErrors).Key);
        Assert.Equal(2, harness.Airings.Object.GetAll().Count);
    }

    [Fact]
    public void MergeAirings_RefusesAMoveThatTakesTheLastAiringOutOfTheWindow()
    {
        using var harness = new Harness();
        var schedule = harness.Service.AddOrUpdateSchedule(harness.Primary, harness.ScheduleData());
        harness.Service.SetAirings(harness.Primary, schedule, [
            new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = DateTime.UtcNow.AddDays(-2) },
            new EpisodeAiringData() { Episode = harness.Episodes[1], AiredAt = DateTime.UtcNow.AddDays(-1) },
        ]);

        // The other airing is still inside the window, so moving this one back
        // is history rather than a schedule the sweep would take.
        harness.Service.MergeAirings(harness.Primary, schedule, [
            new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = DateTime.UtcNow.AddYears(-3) },
        ]);
        Assert.Equal(0, harness.Service.RunRetentionSweep());

        // Moving the last one inside the window back empties the window out.
        var exception = Assert.Throws<AiringScheduleValidationException>(() => harness.Service.MergeAirings(harness.Primary, schedule, [
            new EpisodeAiringData() { Episode = harness.Episodes[1], AiredAt = DateTime.UtcNow.AddYears(-3) },
        ]));
        Assert.Equal("#schedule", Assert.Single(exception.ValidationErrors).Key);
    }

    #endregion

    #region Channels

    [Fact]
    public void FindOrRegisterChannel_NormalisesTheNameToOneChannel()
    {
        using var harness = new Harness();

        var first = harness.Service.FindOrRegisterChannel("TOKYO MX", AiringChannelType.Television);
        var second = harness.Service.FindOrRegisterChannel("  tokyo  mx ", AiringChannelType.Television);
        var wide = harness.Service.FindOrRegisterChannel("ＴＯＫＹＯ　ＭＸ", AiringChannelType.Television);
        var streaming = harness.Service.FindOrRegisterChannel("TOKYO MX", AiringChannelType.Streaming);

        Assert.Equal(first.ID, second.ID);
        Assert.Equal(first.ID, wide.ID);
        Assert.NotEqual(first.ID, streaming.ID);
        // The spelling from the first registration is the one that is kept.
        Assert.Equal("TOKYO MX", second.Name);
    }

    [Fact]
    public void FindOrRegisterChannel_ClaimsANameHeldByAnotherChannelsAlias()
    {
        using var harness = new Harness();
        var owner = harness.Service.FindOrRegisterChannel("Tokyo Metropolitan Television", AiringChannelType.Television);
        harness.Service.AddChannelAliases(owner, ["TOKYO MX"]);

        var claimed = harness.Service.FindOrRegisterChannel("TOKYO MX", AiringChannelType.Television);

        Assert.NotEqual(owner.ID, claimed.ID);
        Assert.Equal(claimed.ID, harness.Service.GetChannelByName("TOKYO MX", AiringChannelType.Television)?.ID);
        Assert.Empty(harness.Service.GetChannelByID(owner.ID)!.Aliases);
    }

    [Fact]
    public void AddChannelAliases_RefusesANameAnotherChannelAlreadyAnswersTo()
    {
        using var harness = new Harness();
        var owner = harness.Service.FindOrRegisterChannel("TOKYO MX", AiringChannelType.Television);
        var other = harness.Service.FindOrRegisterChannel("BS11", AiringChannelType.Television);

        var exception = Assert.Throws<ChannelAliasConflictException>(() => harness.Service.AddChannelAliases(other, ["tokyo mx"]));
        Assert.True(exception.HeldAsOwnName);
        Assert.Equal(owner.ID, exception.ConflictingChannel.ID);

        // An alias equal to the channel's own name is nothing to do, not a conflict.
        Assert.Empty(harness.Service.AddChannelAliases(other, ["BS11"]).Aliases);
    }

    [Fact]
    public void GetChannelByName_SkipsAliasesWhenAsked()
    {
        using var harness = new Harness();
        var owner = harness.Service.FindOrRegisterChannel("Tokyo Metropolitan Television", AiringChannelType.Television);
        harness.Service.AddChannelAliases(owner, ["TOKYO MX"]);

        Assert.Equal(owner.ID, harness.Service.GetChannelByName("TOKYO MX", AiringChannelType.Television)?.ID);
        Assert.Null(harness.Service.GetChannelByName("TOKYO MX", AiringChannelType.Television, useAliases: false));
    }

    #endregion

    #region Airing Notifications

    [Fact]
    public void Tick_DispatchesAnAiringInTheHorizonOnceAndOnlyOnce()
    {
        using var harness = new Harness();
        var now = Harness.Minute(DateTime.UtcNow);
        var tokyo = harness.Service.FindOrRegisterChannel("TOKYO MX", AiringChannelType.Television);
        harness.Schedule(harness.Primary, "mx", tokyo.ID, [new AiringTrackData(AiringKind.Original, "ja")], (0, now.AddMinutes(5)));
        var received = new List<EpisodeAiredEventArgs>();
        using var subscription = harness.Service.SubscribeToAirings(received.Add);

        // The first tick only picks the watermark up; nothing has come due yet.
        harness.Notifications.Tick(now);
        Assert.Empty(received);

        harness.Notifications.Tick(now.AddMinutes(5));
        var dispatch = Assert.Single(received);
        Assert.Equal(now.AddMinutes(5), dispatch.AiredAt);
        Assert.False(Assert.Single(dispatch.Airings).IsEstimated);

        // The watermark is past it now, so a later tick — and the rebuild that
        // comes with it — never hands the same slot out twice.
        harness.Notifications.Tick(now.AddMinutes(6));
        Assert.Single(received);
    }

    [Fact]
    public void Tick_BatchesEverythingInOneMinuteIntoASingleDispatch()
    {
        using var harness = new Harness();
        var now = Harness.Minute(DateTime.UtcNow);
        var tokyo = harness.Service.FindOrRegisterChannel("テレビ愛知", AiringChannelType.Television);
        var tvTokyo = harness.Service.FindOrRegisterChannel("テレビ東京", AiringChannelType.Television);
        var tracks = new[] { new AiringTrackData(AiringKind.Original, "ja") };
        harness.Schedule(harness.Primary, "aichi", tokyo.ID, tracks, (0, now.AddMinutes(5)));
        harness.Schedule(harness.Primary, "tx", tvTokyo.ID, tracks, (0, now.AddMinutes(5)));
        var received = new List<EpisodeAiredEventArgs>();
        using var subscription = harness.Service.SubscribeToAirings(received.Add);

        harness.Notifications.Tick(now);
        harness.Notifications.Tick(now.AddMinutes(5));

        // One episode simulcast on two stations at the same minute is one
        // dispatch carrying both, not two dispatches. Whoever wants "this
        // episode aired, once" groups the list themselves.
        var dispatch = Assert.Single(received);
        Assert.Equal(2, dispatch.Airings.Count);
        Assert.Equal(2, dispatch.Airings.Select(airing => airing.ID).Distinct().Count());
        Assert.Equal(new HashSet<Guid> { tokyo.ID, tvTokyo.ID }, dispatch.Airings.Select(airing => airing.Channel!.ID).ToHashSet());
    }

    [Fact]
    public void Tick_DispatchesAnEstimateAndFlagsItAsOne()
    {
        using var harness = new Harness();
        var now = Harness.Minute(DateTime.UtcNow);
        var tokyo = harness.Service.FindOrRegisterChannel("TOKYO MX", AiringChannelType.Television);
        harness.EstimatedSchedule(now);
        var received = new List<EpisodeAiredEventArgs>();
        using var subscription = harness.Service.SubscribeToAirings(received.Add);

        harness.Notifications.Tick(now);
        harness.Notifications.Tick(now.AddMinutes(5));

        var dispatch = Assert.Single(received);
        Assert.True(Assert.Single(dispatch.Airings).IsEstimated);
        Assert.Equal(now.AddMinutes(5), dispatch.AiredAt);
        Assert.Equal(tokyo.ID, dispatch.Airings[0].Channel!.ID);
    }

    [Fact]
    public void Tick_SkipsForwardInsteadOfReplayingWhatItMissed()
    {
        using var harness = new Harness();
        var now = Harness.Minute(DateTime.UtcNow);
        harness.SeedWatermark(now.AddHours(-3));
        var tokyo = harness.Service.FindOrRegisterChannel("TOKYO MX", AiringChannelType.Television);
        harness.Schedule(harness.Primary, "mx", tokyo.ID, [new AiringTrackData(AiringKind.Original, "ja")], (0, now.AddMinutes(-30)));
        var received = new List<EpisodeAiredEventArgs>();
        using var subscription = harness.Service.SubscribeToAirings(received.Add);

        harness.Notifications.Tick(now);

        // The slot passed while the server was down. An hours-old prediction is
        // worse than none, so it is stepped over rather than announced late.
        Assert.Empty(received);
        Assert.Equal(now, harness.Notifications.Watermark);
        Assert.Equal(now, harness.Watermark?.LastUpdate);
    }

    [Fact]
    public void Tick_RebuildsTheHorizonWhenTheAiringsChangeUnderIt()
    {
        using var harness = new Harness();
        var now = Harness.Minute(DateTime.UtcNow);
        var tokyo = harness.Service.FindOrRegisterChannel("TOKYO MX", AiringChannelType.Television);
        var schedule = harness.Schedule(harness.Primary, "mx", tokyo.ID, [new AiringTrackData(AiringKind.Original, "ja")], (0, now.AddMinutes(10)));
        var received = new List<EpisodeAiredEventArgs>();
        using var subscription = harness.Service.SubscribeToAirings(received.Add);

        harness.Notifications.Tick(now);
        Assert.Single(harness.Notifications.Horizon);

        // The provider moves the slot back by forty minutes, well inside the
        // horizon and well before the periodic rebuild would have noticed.
        harness.Service.SetAirings(harness.Primary, schedule, [
            new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = now.AddMinutes(50) },
        ]);

        harness.Notifications.Tick(now.AddMinutes(10));
        Assert.Empty(received);

        harness.Notifications.Tick(now.AddMinutes(50));
        Assert.Equal(now.AddMinutes(50), Assert.Single(received).AiredAt);
    }

    [Fact]
    public void Tick_StaysIdleWhileNobodyIsSubscribed()
    {
        using var harness = new Harness();
        var now = Harness.Minute(DateTime.UtcNow);
        var tokyo = harness.Service.FindOrRegisterChannel("TOKYO MX", AiringChannelType.Television);
        harness.Schedule(harness.Primary, "mx", tokyo.ID, [new AiringTrackData(AiringKind.Original, "ja")], (0, now.AddMinutes(5)));

        harness.Notifications.Tick(now);

        // Nothing is listening, so nothing is held in memory and the read path
        // — estimates and all — is never run at all.
        Assert.Empty(harness.Notifications.Horizon);
        Assert.Empty(harness.Notifications.Tick(now.AddMinutes(5)));
        Assert.Empty(harness.Notifications.Horizon);

        // The watermark still walks forward, so the first subscriber to turn up
        // is not handed everything it slept through.
        Assert.Equal(now.AddMinutes(5), harness.Notifications.Watermark);
    }

    [Fact]
    public void Tick_ArmsTheHorizonOnTheFirstSubscriberRatherThanTheNextRefresh()
    {
        using var harness = new Harness();
        var now = Harness.Minute(DateTime.UtcNow);
        var tokyo = harness.Service.FindOrRegisterChannel("TOKYO MX", AiringChannelType.Television);
        harness.Schedule(harness.Primary, "mx", tokyo.ID, [new AiringTrackData(AiringKind.Original, "ja")], (0, now.AddMinutes(5)));

        harness.Notifications.Tick(now);
        Assert.Empty(harness.Notifications.Horizon);

        var received = new List<EpisodeAiredEventArgs>();
        using var subscription = harness.Service.SubscribeToAirings(received.Add);

        // The very next tick rebuilds, a quarter of an hour before the periodic
        // refresh would have, because the subscription version moved.
        harness.Notifications.Tick(now.AddMinutes(1));
        Assert.Single(harness.Notifications.Horizon);

        harness.Notifications.Tick(now.AddMinutes(5));
        Assert.Single(Assert.Single(received).Airings);
    }

    [Fact]
    public void Tick_SkipsTheEstimatesWhenNoSubscriberWantsThem()
    {
        using var harness = new Harness();
        var now = Harness.Minute(DateTime.UtcNow);
        harness.EstimatedSchedule(now);
        var received = new List<EpisodeAiredEventArgs>();
        using var subscription = harness.Service.SubscribeToAirings(received.Add, new EpisodeAiringFilteringOptions() { IncludeEstimates = false });

        harness.Notifications.Tick(now);

        // The only thing the hour holds is an estimate, and estimates are
        // computed through the read path rather than stored, so a horizon
        // nobody wants them in never computes one.
        Assert.Empty(harness.Notifications.Horizon);
        Assert.Empty(harness.Notifications.Tick(now.AddMinutes(5)));
        Assert.Empty(received);
    }

    [Fact]
    public void Tick_UnionsTheHorizonOverEveryLiveSubscriber()
    {
        using var harness = new Harness();
        var now = Harness.Minute(DateTime.UtcNow);
        harness.EstimatedSchedule(now);
        var withoutEstimates = new List<EpisodeAiredEventArgs>();
        var withEstimates = new List<EpisodeAiredEventArgs>();
        using var first = harness.Service.SubscribeToAirings(withoutEstimates.Add, new EpisodeAiringFilteringOptions() { IncludeEstimates = false });
        using var second = harness.Service.SubscribeToAirings(withEstimates.Add, new EpisodeAiringFilteringOptions() { IncludeEstimates = true });

        harness.Notifications.Tick(now);
        harness.Notifications.Tick(now.AddMinutes(5));

        // One subscriber wanting estimates is enough for the horizon to hold
        // them, and the one that didn't ask still never sees them.
        Assert.Single(Assert.Single(withEstimates).Airings);
        Assert.Empty(withoutEstimates);
    }

    [Fact]
    public void SubscribeToAirings_FiltersPerSubscriber()
    {
        using var harness = new Harness();
        var now = Harness.Minute(DateTime.UtcNow);
        var tokyo = harness.Service.FindOrRegisterChannel("TOKYO MX", AiringChannelType.Television);
        var bs11 = harness.Service.FindOrRegisterChannel("BS11", AiringChannelType.Television);
        var tracks = new[] { new AiringTrackData(AiringKind.Original, "ja") };
        harness.Schedule(harness.Primary, "mx", tokyo.ID, tracks, (0, now.AddMinutes(5)));
        harness.Schedule(harness.Primary, "bs11", bs11.ID, tracks, (0, now.AddMinutes(5)));
        var tokyoOnly = new List<EpisodeAiredEventArgs>();
        var everything = new List<EpisodeAiredEventArgs>();
        using var first = harness.Service.SubscribeToAirings(
            tokyoOnly.Add,
            new EpisodeAiringFilteringOptions() { ChannelIDs = new HashSet<Guid> { tokyo.ID } }
        );
        using var second = harness.Service.SubscribeToAirings(everything.Add);

        harness.Notifications.Tick(now);
        harness.Notifications.Tick(now.AddMinutes(5));

        // One horizon, two answers: the filters are the subscriber's, not the
        // ticker's.
        Assert.Equal(tokyo.ID, Assert.Single(Assert.Single(tokyoOnly).Airings).Channel!.ID);
        Assert.Equal(2, Assert.Single(everything).Airings.Count);
    }

    [Fact]
    public void SubscribeToAirings_StopsDispatchingOnceDisposed()
    {
        using var harness = new Harness();
        var now = Harness.Minute(DateTime.UtcNow);
        var tokyo = harness.Service.FindOrRegisterChannel("TOKYO MX", AiringChannelType.Television);
        var tracks = new[] { new AiringTrackData(AiringKind.Original, "ja") };
        harness.Schedule(harness.Primary, "mx", tokyo.ID, tracks, (0, now.AddMinutes(5)), (1, now.AddMinutes(10)));
        var leaving = new List<EpisodeAiredEventArgs>();
        var staying = new List<EpisodeAiredEventArgs>();
        var subscription = harness.Service.SubscribeToAirings(leaving.Add);
        using var other = harness.Service.SubscribeToAirings(staying.Add);

        harness.Notifications.Tick(now);
        harness.Notifications.Tick(now.AddMinutes(5));
        Assert.Single(leaving);

        subscription.Dispose();
        // Disposing twice, from anywhere, is a no-op rather than a second
        // removal or a throw.
        subscription.Dispose();

        harness.Notifications.Tick(now.AddMinutes(10));
        Assert.Single(leaving);
        Assert.Equal(2, staying.Count);
    }

    [Fact]
    public void SubscribeToAirings_KeepsGoingWhenOneSubscriberThrows()
    {
        using var harness = new Harness();
        var now = Harness.Minute(DateTime.UtcNow);
        var tokyo = harness.Service.FindOrRegisterChannel("TOKYO MX", AiringChannelType.Television);
        harness.Schedule(harness.Primary, "mx", tokyo.ID, [new AiringTrackData(AiringKind.Original, "ja")], (0, now.AddMinutes(5)));
        var received = new List<EpisodeAiredEventArgs>();
        using var bad = harness.Service.SubscribeToAirings(_ => throw new InvalidOperationException("A bad plugin."));
        using var good = harness.Service.SubscribeToAirings(received.Add);

        harness.Notifications.Tick(now);
        var due = harness.Notifications.Tick(now.AddMinutes(5));

        // A plugin that throws costs itself its dispatch, and neither the tick
        // nor the subscribers behind it.
        Assert.Single(due);
        Assert.Single(Assert.Single(received).Airings);
    }

    #endregion

    #region Entity Anchor

    [Fact]
    public void GetAiringsForEpisode_AutoAnchorsToTheEntityItWasGiven()
    {
        using var harness = new Harness();
        var tokyo = harness.Service.FindOrRegisterChannel("TOKYO MX", AiringChannelType.Television);
        harness.OrphanSchedule(harness.Primary, "orphan", tokyo.ID, harness.Air(1));

        // The episode has no shoko episode behind it, so Auto infers Raw from
        // it and the airing is returned exactly as the provider stored it.
        var auto = harness.Service.GetAiringsForEpisode(harness.OrphanEpisode, new EpisodeAiringFilteringOptions() { IncludeEstimates = false });
        Assert.Single(auto);

        var raw = harness.Service.GetAiringsForEpisode(harness.OrphanEpisode, new EpisodeAiringFilteringOptions()
        {
            IncludeEstimates = false,
            EntityAnchor = AiringEntityAnchor.Raw,
        });
        Assert.Equal(auto.Select(airing => airing.ID), raw.Select(airing => airing.ID));
    }

    [Fact]
    public void GetAiringsForEpisode_ShokoAnchorDropsWhatResolvesToNoShokoEpisode()
    {
        using var harness = new Harness();
        var tokyo = harness.Service.FindOrRegisterChannel("TOKYO MX", AiringChannelType.Television);
        harness.OrphanSchedule(harness.Primary, "orphan", tokyo.ID, harness.Air(1));

        Assert.Empty(harness.Service.GetAiringsForEpisode(harness.OrphanEpisode, new EpisodeAiringFilteringOptions()
        {
            IncludeEstimates = false,
            EntityAnchor = AiringEntityAnchor.Shoko,
        }));
    }

    [Fact]
    public void GetAiringsForEpisode_AutoAnchorsAShokoEpisodeToShoko()
    {
        using var harness = new Harness();
        var tokyo = harness.Service.FindOrRegisterChannel("TOKYO MX", AiringChannelType.Television);
        harness.Schedule(harness.Primary, "mx", tokyo.ID, [new AiringTrackData(AiringKind.Original, "ja")], (0, harness.Air(1)));

        // The provider stored the airing against its own episode; asking from
        // the shoko side still reaches it, and every airing carries the shoko
        // episode the read was anchored to.
        var airings = harness.Service.GetAiringsForEpisode(harness.ShokoEpisodes[0], new EpisodeAiringFilteringOptions() { IncludeEstimates = false });
        Assert.Equal(harness.ShokoEpisodes[0].ID, Assert.Single(airings).ShokoEpisode?.ID);
    }

    [Fact]
    public void GetAiringsInRange_AutoFallsBackToRawWithNoEntityToInferFrom()
    {
        using var harness = new Harness();
        var tokyo = harness.Service.FindOrRegisterChannel("TOKYO MX", AiringChannelType.Television);
        var airedAt = harness.Air(1);
        harness.OrphanSchedule(harness.Primary, "orphan", tokyo.ID, airedAt);
        var from = airedAt.AddHours(-1);
        var to = airedAt.AddHours(1);
        var options = new EpisodeAiringFilteringOptions() { IncludeEstimates = false };

        // Nothing was passed in to infer an anchor from, so Auto reads Raw and
        // the range keeps what it always kept.
        Assert.Single(harness.Service.GetAiringsInRange(from, to, options));
        Assert.Empty(harness.Service.GetAiringsInRange(from, to, new EpisodeAiringFilteringOptions()
        {
            IncludeEstimates = false,
            EntityAnchor = AiringEntityAnchor.Shoko,
        }));
    }

    [Fact]
    public void GetSchedulesForSeries_ShokoAnchorDropsWhatResolvesToNoShokoSeries()
    {
        using var harness = new Harness();
        var tokyo = harness.Service.FindOrRegisterChannel("TOKYO MX", AiringChannelType.Television);
        harness.OrphanSchedule(harness.Primary, "orphan", tokyo.ID, harness.Air(1));

        Assert.Single(harness.Service.GetSchedulesForSeries(harness.OrphanSeries));
        Assert.Empty(harness.Service.GetSchedulesForSeries(harness.OrphanSeries, new AiringScheduleFilteringOptions()
        {
            EntityAnchor = AiringEntityAnchor.Shoko,
        }));
    }

    #endregion

    #region Harness

    /// <summary>
    ///   Everything one test needs: the repositories installed into <c>RepoFactory</c>, two
    ///   registered providers, a resolver for the entity doubles, and a small vocabulary for
    ///   building schedules and airings.
    /// </summary>
    private sealed class Harness : IDisposable
    {
        private readonly RepoFactoryScope _scope = new();

        private int _nextScheduleID = 1;

        private int _nextAiringID = 1;

        private static readonly DateTime _firstAirDate = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(-30), DateTimeKind.Utc);

        public AiringScheduleServiceSettings Settings { get; } = new();

        public Mock<AiringScheduleRepository> Schedules { get; }

        public Mock<EpisodeAiringRepository> Airings { get; }

        public Mock<AiringChannelRepository> Channels { get; }

        public Mock<ScheduledUpdateRepository> Updates { get; }

        public AiringScheduleService Service { get; }

        /// <summary>The background ticker, wired to the same service instance.</summary>
        public EpisodeAiringNotificationService Notifications { get; }

        /// <summary>The persisted "fired through" row, or <c>null</c> when nothing has written one.</summary>
        public ScheduledUpdate? Watermark { get; private set; }

        public TestProvider Primary { get; } = new("Alpha");

        public SecondaryProvider Secondary { get; } = new("Beta");

        /// <summary>The provider-side episodes every airing is stored against.</summary>
        public IReadOnlyList<IEpisode> Episodes { get; }

        /// <summary>The shoko episodes those provider episodes are linked to.</summary>
        public IReadOnlyList<IShokoEpisode> ShokoEpisodes { get; }

        /// <summary>The provider-side series every schedule is attached to.</summary>
        public ISeries ProviderSeries { get; }

        /// <summary>The shoko series the provider-side one is linked to.</summary>
        public IShokoSeries ShokoSeries { get; }

        /// <summary>A provider-side series with nothing in the collection behind it, for the anchor.</summary>
        public ISeries OrphanSeries { get; }

        /// <summary>The one episode of <see cref="OrphanSeries"/>, which resolves to no shoko episode at all.</summary>
        public IEpisode OrphanEpisode { get; }

        public Harness()
        {
            Schedules = CachedRepo.BuildWritable<AiringScheduleRepository, int, AiringSchedule>(entry => entry.AiringScheduleID);
            Schedules.Setup(repository => repository.Save(It.IsAny<AiringSchedule>())).Callback<AiringSchedule>(entry =>
            {
                if (entry.AiringScheduleID is 0)
                    entry.AiringScheduleID = _nextScheduleID++;
                Schedules.Object.Cache.Update(entry);
            });
            Airings = CachedRepo.BuildWritable<EpisodeAiringRepository, int, EpisodeAiring>(entry => entry.EpisodeAiringID);
            Airings.Setup(repository => repository.Save(It.IsAny<EpisodeAiring>())).Callback<EpisodeAiring>(entry =>
            {
                if (entry.EpisodeAiringID is 0)
                    entry.EpisodeAiringID = _nextAiringID++;
                Airings.Object.Cache.Update(entry);
            });
            Airings.Setup(repository => repository.Delete(It.IsAny<IReadOnlyCollection<EpisodeAiring>>()))
                .Callback<IReadOnlyCollection<EpisodeAiring>>(entries =>
                {
                    foreach (var entry in entries.ToList())
                        Airings.Object.Cache.Remove(entry);
                });
            Channels = CachedRepo.BuildWritable<AiringChannelRepository, int, AiringChannel>(entry => entry.AiringChannelID);
            var nextChannelID = 1;
            Channels.Setup(repository => repository.Save(It.IsAny<AiringChannel>())).Callback<AiringChannel>(entry =>
            {
                if (entry.AiringChannelID is 0)
                    entry.AiringChannelID = nextChannelID++;
                Channels.Object.Cache.Update(entry);
            });
            // The ticker's watermark is an ordinary ScheduledUpdate row, so the
            // direct repository is mocked down to the two calls it makes.
            Updates = new Mock<ScheduledUpdateRepository>((DatabaseFactory)null!);
            Updates.Setup(repository => repository.GetByUpdateType(It.IsAny<int>())).Returns(() => Watermark);
            Updates.Setup(repository => repository.Save(It.IsAny<ScheduledUpdate>())).Callback<ScheduledUpdate>(row => Watermark = row);
            _scope.Set(Schedules.Object).Set(Airings.Object).Set(Channels.Object).Set(Updates.Object);

            var shokoSeries = new Mock<IShokoSeries>();
            var linkedSeries = new Mock<ISeries>();
            linkedSeries.SetupGet(series => series.ID).Returns(2);
            linkedSeries.SetupGet(series => series.Source).Returns(DataSource.Plugin);
            linkedSeries.SetupGet(series => series.EntityType).Returns(DataEntityType.Series);
            shokoSeries.SetupGet(series => series.ID).Returns(1);
            shokoSeries.SetupGet(series => series.Source).Returns(DataSource.Plugin);
            shokoSeries.SetupGet(series => series.EntityType).Returns(DataEntityType.Series);
            shokoSeries.SetupGet(series => series.LinkedSeries).Returns([linkedSeries.Object]);
            ProviderSeries = linkedSeries.Object;
            ShokoSeries = shokoSeries.Object;

            var episodes = new List<IEpisode>();
            var shokoEpisodes = new List<IShokoEpisode>();
            for (var index = 0; index < 4; index++)
            {
                var episode = new Mock<IEpisode>();
                var shokoEpisode = new Mock<IShokoEpisode>();
                episode.SetupGet(entry => entry.ID).Returns(100 + index);
                episode.SetupGet(entry => entry.Source).Returns(DataSource.Plugin);
                episode.SetupGet(entry => entry.EntityType).Returns(DataEntityType.Episode);
                episode.SetupGet(entry => entry.SeriesID).Returns(2);
                episode.SetupGet(entry => entry.Type).Returns(EpisodeType.Episode);
                episode.SetupGet(entry => entry.EpisodeNumber).Returns(index + 1);
                episode.SetupGet(entry => entry.AirDate).Returns(DateOnly.FromDateTime(_firstAirDate.AddDays(7 * index)));
                episode.SetupGet(entry => entry.ShokoEpisodes).Returns(() => [shokoEpisode.Object]);
                shokoEpisode.SetupGet(entry => entry.ID).Returns(200 + index);
                shokoEpisode.SetupGet(entry => entry.Source).Returns(DataSource.Plugin);
                shokoEpisode.SetupGet(entry => entry.EntityType).Returns(DataEntityType.Episode);
                shokoEpisode.SetupGet(entry => entry.SeriesID).Returns(1);
                shokoEpisode.SetupGet(entry => entry.Type).Returns(EpisodeType.Episode);
                shokoEpisode.SetupGet(entry => entry.EpisodeNumber).Returns(index + 1);
                shokoEpisode.SetupGet(entry => entry.AirDate).Returns(DateOnly.FromDateTime(_firstAirDate.AddDays(7 * index)));
                shokoEpisode.SetupGet(entry => entry.LinkedEpisodes).Returns(() => [episode.Object]);
                shokoEpisode.SetupGet(entry => entry.ShokoEpisodes).Returns(() => [shokoEpisode.Object]);
                episodes.Add(episode.Object);
                shokoEpisodes.Add(shokoEpisode.Object);
            }

            // Its own series, so it never joins the runs the rest of the tests
            // read, estimate over or count.
            var orphanSeries = new Mock<ISeries>();
            orphanSeries.SetupGet(series => series.ID).Returns(3);
            orphanSeries.SetupGet(series => series.Source).Returns(DataSource.Plugin);
            orphanSeries.SetupGet(series => series.EntityType).Returns(DataEntityType.Series);
            orphanSeries.SetupGet(series => series.ShokoSeries).Returns([]);
            var orphanEpisode = new Mock<IEpisode>();
            orphanEpisode.SetupGet(entry => entry.ID).Returns(300);
            orphanEpisode.SetupGet(entry => entry.Source).Returns(DataSource.Plugin);
            orphanEpisode.SetupGet(entry => entry.EntityType).Returns(DataEntityType.Episode);
            orphanEpisode.SetupGet(entry => entry.SeriesID).Returns(3);
            orphanEpisode.SetupGet(entry => entry.Type).Returns(EpisodeType.Episode);
            orphanEpisode.SetupGet(entry => entry.EpisodeNumber).Returns(1);
            orphanEpisode.SetupGet(entry => entry.AirDate).Returns(DateOnly.FromDateTime(_firstAirDate));
            orphanEpisode.SetupGet(entry => entry.ShokoEpisodes).Returns([]);
            orphanSeries.SetupGet(series => series.Episodes).Returns(() => [orphanEpisode.Object]);
            OrphanSeries = orphanSeries.Object;
            OrphanEpisode = orphanEpisode.Object;

            Episodes = episodes;
            ShokoEpisodes = shokoEpisodes;
            shokoSeries.SetupGet(series => series.Episodes).Returns(() => shokoEpisodes);
            linkedSeries.SetupGet(series => series.Episodes).Returns(() => episodes);

            var configurationInfo = (ConfigurationInfo)RuntimeHelpers.GetUninitializedObject(typeof(ConfigurationInfo));
            var configurationService = new Mock<IConfigurationService>();
            configurationService.Setup(service => service.GetConfigurationInfo<AiringScheduleServiceSettings>()).Returns(configurationInfo);
            configurationService.Setup(service => service.Load(It.IsAny<ConfigurationInfo>(), It.IsAny<bool>())).Returns(Settings);
            var pluginManager = new Mock<IPluginManager>();
            pluginManager.Setup(manager => manager.GetPluginInfo(It.IsAny<Assembly>()))
                .Returns(PluginTestDoubles.CorePluginInfo(typeof(CorePlugin), CorePlugin.StaticID));

            Service = new AiringScheduleService(
                NullLogger<AiringScheduleService>.Instance,
                configurationService.Object,
                pluginManager.Object,
                new Mock<IQueueScheduler>().Object,
                new ConfigurationProvider<AiringScheduleServiceSettings>(configurationService.Object)
            );
            Service.AddParts([Primary, Secondary], [new TestEntityResolver(this)]);
            // Built here rather than per test, so it is subscribed to the
            // service's change events before anything writes a schedule.
            Notifications = new EpisodeAiringNotificationService(
                NullLogger<EpisodeAiringNotificationService>.Instance,
                Service,
                new Mock<ISystemService>().Object
            );
        }

        /// <summary>The UTC minute <paramref name="value"/> falls in, which is the grid the ticker works on.</summary>
        public static DateTime Minute(DateTime value)
            => new(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0, DateTimeKind.Utc);

        /// <summary>Pretends the ticker last dispatched events through <paramref name="lastUpdate"/>.</summary>
        public void SeedWatermark(DateTime lastUpdate)
            => Watermark = new ScheduledUpdate
            {
                UpdateType = (int)ScheduledUpdateType.EpisodeAiringNotifications,
                UpdateDetails = string.Empty,
                LastUpdate = lastUpdate,
            };

        /// <summary>
        ///   A UTC instant on one of the run's days, counted from the first episode's air date.
        ///   The run sits just inside the retention window, so a write isn't refused for being
        ///   older than it.
        /// </summary>
        public DateTime Air(int day, int hour = 15, int minute = 0)
            => _firstAirDate.AddDays(day - 1).AddHours(hour).AddMinutes(minute);

        public AiringScheduleData ScheduleData(
            string? key = null,
            Guid? channelID = null,
            IReadOnlyList<AiringTrackData>? tracks = null,
            string? url = null,
            int? lastEpisodeNumber = null,
            bool shokoEntities = false,
            ISeries? series = null
        )
            => new()
            {
                Series = series ?? (shokoEntities ? ShokoSeries : ProviderSeries),
                ChannelID = channelID,
                Tracks = tracks ?? [new AiringTrackData(AiringKind.Original, "ja")],
                Key = key,
                Url = url,
                LastEpisodeNumber = lastEpisodeNumber,
            };

        /// <summary>
        ///   A schedule with airings on it, which is the shape most reads are asserted against.
        /// </summary>
        public IAiringSchedule Schedule(
            IAiringScheduleProvider provider,
            string key,
            Guid? channelID,
            IReadOnlyList<AiringTrackData> tracks,
            params (int Episode, DateTime AiredAt)[] airings
        )
            => Schedule(provider, key, channelID, tracks, shokoEntities: false, airings);

        public IAiringSchedule Schedule(
            IAiringScheduleProvider provider,
            string key,
            Guid? channelID,
            IReadOnlyList<AiringTrackData> tracks,
            bool shokoEntities,
            params (int Episode, DateTime AiredAt)[] airings
        )
        {
            var schedule = Service.AddOrUpdateSchedule(provider, ScheduleData(key, channelID, tracks, shokoEntities: shokoEntities));
            if (airings.Length > 0)
                Service.SetAirings(provider, schedule, airings.Select(entry => new EpisodeAiringData()
                {
                    Episode = shokoEntities ? ShokoEpisodes[entry.Episode] : Episodes[entry.Episode],
                    AiredAt = entry.AiredAt,
                }));

            return schedule;
        }

        /// <summary>
        ///   One channel's own run of repeats of a single episode: several slots on one schedule,
        ///   each under its own key, which is how a provider lists a late-night rerun.
        /// </summary>
        public IAiringSchedule Repeats(Guid? channelID, int episode, params DateTime[] slots)
        {
            var schedule = Service.AddOrUpdateSchedule(Primary, ScheduleData("repeats", channelID));
            Service.SetAirings(Primary, schedule, slots.Select((airedAt, index) => new EpisodeAiringData()
            {
                Key = $"{episode}-{index}",
                Episode = Episodes[episode],
                AiredAt = airedAt,
            }));
            return schedule;
        }

        /// <summary>
        ///   A schedule on <see cref="OrphanSeries"/> with its one airing, which is what the
        ///   anchor has to drop when it is told to answer with shoko entities.
        /// </summary>
        public IAiringSchedule OrphanSchedule(IAiringScheduleProvider provider, string key, Guid? channelID, DateTime airedAt)
        {
            var schedule = Service.AddOrUpdateSchedule(provider, ScheduleData(key, channelID, series: OrphanSeries));
            Service.SetAirings(provider, schedule, [new EpisodeAiringData() { Episode = OrphanEpisode, AiredAt = airedAt }]);
            return schedule;
        }

        /// <summary>
        ///   A weekly run whose two real airings sit a fixed distance from their AniDB dates, so
        ///   the slot it learns puts the fourth episode's estimate five minutes after
        ///   <paramref name="now"/> and nothing real lands in the ticker's hour.
        /// </summary>
        public IAiringSchedule EstimatedSchedule(DateTime now)
        {
            var tokyo = Service.FindOrRegisterChannel("TOKYO MX", AiringChannelType.Television);
            var offset = now.AddMinutes(5) - Air(22, 0, 0);
            return Schedule(
                Primary,
                "mx",
                tokyo.ID,
                [new AiringTrackData(AiringKind.Original, "ja")],
                (0, Air(1, 0, 0) + offset),
                (1, Air(8, 0, 0) + offset)
            );
        }

        public void Dispose()
        {
            Notifications.Dispose();
            _scope.Dispose();
        }

        /// <summary>
        ///   Resolves the Moq entities back from the keys the service stored them under, the way a
        ///   plugin's own resolver does for its entities.
        /// </summary>
        private sealed class TestEntityResolver(Harness harness) : IAiringScheduleEntityResolver
        {
            public string Name => "Test Entities";

            public IMetadata? GetEntity(DataSource source, DataEntityType type, string id)
                => type switch
                {
                    DataEntityType.Series => new[] { harness.ProviderSeries, harness.ShokoSeries, harness.OrphanSeries }
                        .FirstOrDefault(entity => entity.ID.ToString() == id),
                    DataEntityType.Episode => harness.Episodes.Cast<IMetadata>()
                        .Concat(harness.ShokoEpisodes)
                        .Append(harness.OrphanEpisode)
                        .FirstOrDefault(entity => entity is IEpisode episode && episode.ID.ToString() == id),
                    _ => null,
                };

            public IEnumerable<IMetadata> GetLinkedEntities(IMetadata shokoEntity)
                => [];
        }
    }

    /// <summary>A provider that only exists to be registered and owned things.</summary>
    private class TestProvider(string name) : IAiringScheduleProvider
    {
        public string Name { get; } = name;

        public IReadOnlySet<AiringKind> AvailableKinds { get; } = new HashSet<AiringKind> { AiringKind.Original, AiringKind.Subtitled };

        public Task<bool> RefreshAsync(ISeries series, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }

    /// <summary>A second provider, so ownership and priority have two sides to them.</summary>
    private sealed class SecondaryProvider(string name) : TestProvider(name);

    #endregion
}
