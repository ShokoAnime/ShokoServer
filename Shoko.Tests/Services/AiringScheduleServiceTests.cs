using System;
using System.Collections.Generic;
using System.Globalization;
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
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Filtering;
using Shoko.Abstractions.Filtering.Services;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Events;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.User;
using Shoko.Abstractions.Utilities;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Databases;
using Shoko.Server.Models.Airing;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Internal;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Plugin;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.Airing;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Server;
using Shoko.Server.Services;
using Shoko.Server.Services.Airing;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;
using Shoko.Tests.Actors;
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
///   Entities are Moq doubles resolved through a mocked <see cref="IMetadataService.GetEntry(MetadataGuid)"/>,
///   which keeps the tests clear of AniDB, TMDB and shoko rows the service never reads.
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
    public void AScheduleNamesTheProvidersSeries_AndItsAiringsTheirEpisodes()
    {
        using var harness = new Harness();
        var schedule = harness.Service.AddOrUpdateSchedule(harness.Primary, harness.ScheduleData());
        var airing = Assert.Single(harness.Service.SetAirings(harness.Primary, schedule, [
            new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = harness.Air(38) },
        ]));

        Assert.Equal(harness.ProviderSeries.ID, schedule.SeriesID);
        Assert.Null(schedule.SeasonID);
        Assert.Equal(harness.Episodes[0].ID, airing.EpisodeID);
    }

    [Fact]
    public void AnAiringKeepsTheKindItWasSubmittedWith()
    {
        using var harness = new Harness();
        var schedule = harness.Service.AddOrUpdateSchedule(harness.Primary, harness.ScheduleData());
        var written = harness.Service.SetAirings(harness.Primary, schedule, [
            new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = harness.Air(38) },
            new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = harness.Air(31), Key = "advance", Kind = EpisodeAiringKind.Advance },
            new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = harness.Air(60), Key = "rerun", Kind = EpisodeAiringKind.Rerun },
        ]);

        Assert.Equal(EpisodeAiringKind.Normal, Assert.Single(written, airing => airing.Key != "advance" && airing.Key != "rerun").Kind);
        Assert.Equal(EpisodeAiringKind.Advance, Assert.Single(written, airing => airing.Key == "advance").Kind);
        Assert.Equal(EpisodeAiringKind.Rerun, Assert.Single(written, airing => airing.Key == "rerun").Kind);

        // Only the kind changing is still a change.
        var events = new List<EpisodeAiringsUpdatedEventArgs>();
        harness.Service.AiringsUpdated += (_, e) => events.Add(e);
        harness.Service.MergeAirings(harness.Primary, schedule, [
            new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = harness.Air(60), Key = "rerun", Kind = EpisodeAiringKind.Normal },
        ]);
        Assert.Equal(EpisodeAiringKind.Normal, Assert.Single(Assert.Single(events).Updated).Kind);
    }

    [Theory]
    [InlineData((EpisodeAiringKind)9)]
    [InlineData(EpisodeAiringKind.DetectedRerun)]
    public void SetAirings_RefusesAKindAProviderCannotGive(EpisodeAiringKind kind)
    {
        using var harness = new Harness();
        var schedule = harness.Service.AddOrUpdateSchedule(harness.Primary, harness.ScheduleData());

        var exception = Assert.Throws<AiringScheduleValidationException>(() => harness.Service.SetAirings(
            harness.Primary,
            schedule,
            [new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = harness.Air(38), Kind = kind }]
        ));
        Assert.Single(exception.ValidationErrors);
    }

    [Fact]
    public void AddOrUpdateSchedule_RefusesAChannelChangeForTheSameIdentity()
    {
        using var harness = new Harness();
        var other = harness.Service.FindOrRegisterChannel("BS11", AiringChannelType.Television);
        harness.Service.AddOrUpdateSchedule(harness.Primary, harness.ScheduleData(key: "run"));

        Assert.Throws<ArgumentException>(() => harness.Service.AddOrUpdateSchedule(
            harness.Primary,
            harness.ScheduleData(key: "run", channelID: other.ChannelID)
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
        var hiatus = Assert.Single(after, entry => entry.EpisodeID == harness.Episodes[1].ID);
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
        var untouched = before.Single(airing => airing.EpisodeID == harness.Episodes[0].ID);
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
        Assert.Equal(harness.Episodes[3].ID, Assert.Single(raised.Added).EpisodeID);
        Assert.Equal(harness.Episodes[1].ID, Assert.Single(raised.Updated).EpisodeID);
        Assert.Equal(harness.Episodes[2].ID, Assert.Single(raised.Withdrawn).EpisodeID);

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
        Assert.Equal(harness.Episodes[2].ID, Assert.Single(raised.Added).EpisodeID);
        Assert.Empty(raised.Updated);
        Assert.Equal(2, raised.Withdrawn.Count);

        // A slot still ahead of us is kept without one, so it is off the line
        // rather than gone, and it is reported under the same heading as ...
        var after = harness.Service.GetAiringsForSchedule(schedule.ID, new EpisodeAiringFilteringOptions() { IncludeEstimates = false });
        var hiatus = Assert.Single(raised.Withdrawn, airing => airing.EpisodeID == harness.Episodes[1].ID);
        Assert.Null(hiatus.AiredAt);
        Assert.Equal(harness.Air(45), hiatus.OriginalAiredAt);
        Assert.Contains(after, entry => entry.ID == hiatus.ID);

        // ... the one whose slot has passed, which is history, and history goes.
        var history = Assert.Single(raised.Withdrawn, airing => airing.EpisodeID == harness.Episodes[0].ID);
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
            .Single(airing => airing.EpisodeID == harness.Episodes[2].ID);
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

        static List<(MetadataGuid EpisodeID, DateTime? AiredAt, DateTime? OriginalAiredAt, bool IsDelayed)> Describe(IReadOnlyList<IEpisodeAiring> airings)
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
        harness.Schedule(harness.Primary, "mx", tokyo.ChannelID, [new AiringTrackData(AiringKind.Original, "ja")], (0, harness.Air(1, 14)));
        harness.Schedule(harness.Primary, "cr", crunchyroll.ChannelID, [new AiringTrackData(AiringKind.Subtitled, "en")], (0, harness.Air(1, 15)));

        var subtitled = harness.Service.GetAiringForEpisode(harness.Episodes[0], new EpisodeAiringFilteringOptions()
        {
            PreferredTracks = [new AiringTrackPreference(AiringKind.Subtitled, "en")],
            PreferredChannels = [tokyo.ChannelID],
        });

        // The track preference outranks the channel preference, so the later
        // subtitled release wins over the preferred station's broadcast.
        Assert.NotNull(subtitled);
        Assert.Equal(crunchyroll.ChannelID, subtitled.Channel?.ChannelID);
    }

    [Fact]
    public void GetAiringForEpisode_WithoutAPreferenceTakesTheEarliestAiring()
    {
        using var harness = new Harness();
        var tokyo = harness.Service.FindOrRegisterChannel("TOKYO MX", AiringChannelType.Television);
        var bs11 = harness.Service.FindOrRegisterChannel("BS11", AiringChannelType.Television);
        harness.Schedule(harness.Primary, "bs11", bs11.ChannelID, [new AiringTrackData(AiringKind.Original, "ja")], (0, harness.Air(1, 23, 30)));
        harness.Schedule(harness.Primary, "mx", tokyo.ChannelID, [new AiringTrackData(AiringKind.Original, "ja")], (0, harness.Air(1, 23)));

        var earliest = harness.Service.GetAiringForEpisode(harness.Episodes[0], new EpisodeAiringFilteringOptions()
        {
            PreferredChannels = [],
            PreferredTracks = [],
        });

        Assert.NotNull(earliest);
        Assert.Equal(tokyo.ChannelID, earliest.Channel?.ChannelID);
    }

    [Fact]
    public void GetAiringsForEpisode_FiltersByChannel()
    {
        using var harness = new Harness();
        var tokyo = harness.Service.FindOrRegisterChannel("TOKYO MX", AiringChannelType.Television);
        var bs11 = harness.Service.FindOrRegisterChannel("BS11", AiringChannelType.Television);
        harness.Schedule(harness.Primary, "mx", tokyo.ChannelID, [new AiringTrackData(AiringKind.Original, "ja")], (0, harness.Air(1, 23)));
        harness.Schedule(harness.Primary, "bs11", bs11.ChannelID, [new AiringTrackData(AiringKind.Original, "ja")], (0, harness.Air(1, 23, 30)));

        var airings = harness.Service.GetAiringsForEpisode(harness.Episodes[0], new EpisodeAiringFilteringOptions()
        {
            ChannelIDs = new HashSet<Guid> { bs11.ChannelID },
            IncludeEstimates = false,
        });

        var airing = Assert.Single(airings);
        Assert.Equal(bs11.ChannelID, airing.Channel?.ChannelID);
    }

    [Fact]
    public void GetAiringsForEpisode_KeepsTheHigherPriorityProvidersAiringForOneChannel()
    {
        using var harness = new Harness();
        var crunchyroll = harness.Service.FindOrRegisterChannel("Crunchyroll", AiringChannelType.Streaming);
        var tracks = new[] { new AiringTrackData(AiringKind.Subtitled, "en") };
        harness.Schedule(harness.Primary, "first", crunchyroll.ChannelID, tracks, (0, harness.Air(1, 15)));
        harness.Schedule(harness.Secondary, "second", crunchyroll.ChannelID, tracks, (0, harness.Air(1, 16)));

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
            tokyo.ChannelID,
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
    public void GetAiringsForEpisode_EstimatesAnEpisodeTheSchedulesSourceDoesNotListYet()
    {
        using var harness = new Harness();
        var crunchyroll = harness.Service.FindOrRegisterChannel("Crunchyroll", AiringChannelType.Streaming);
        var schedule = harness.Schedule(
            harness.Primary,
            "crunchyroll",
            crunchyroll.ChannelID,
            [new AiringTrackData(AiringKind.Original, "ja")],
            (0, harness.Air(1, 14, 30)),
            (1, harness.Air(8, 14, 30))
        );
        var anidbEpisodes = harness.UseLinkedAnime(1, (1, 1), (2, 2));

        var third = Assert.Single(harness.Service.GetAiringsForEpisode(anidbEpisodes[2]));
        var fourth = Assert.Single(harness.Service.GetAiringsForEpisode(anidbEpisodes[3], new EpisodeAiringFilteringOptions() { IncludeDateOnly = true }));

        Assert.Equal((true, schedule.ID, harness.Air(15, 14, 30)), (third.IsEstimated, third.Schedule?.ID, third.AiredAt));
        Assert.Equal(new MetadataGuid(MetadataSource.AniDB, MetadataEntityType.Episode, "8003"), third.EpisodeID);
        Assert.Equal((true, false, harness.Air(22, 14, 30)), (fourth.IsEstimated, fourth.IsDateOnly, fourth.AiredAt));
        Assert.Empty(harness.Service.GetAiringsForEpisode(anidbEpisodes[1]));
        Assert.Equal(third.AiredAt, harness.Service.GetAiringByID(third.ID)?.AiredAt);
        Assert.Contains(harness.Service.GetAiringsForSchedule(schedule.ID), airing => airing.ID == fourth.ID && airing.AiredAt == fourth.AiredAt);
        Assert.Contains(harness.Service.GetAiringsInRange(harness.Air(22, 0), harness.Air(23, 0)), airing => airing.ID == fourth.ID);
    }

    [Theory]
    [InlineData(1, 1, 2, 3)]
    [InlineData(1, 2, 2, 2)]
    [InlineData(1, 1, 3, 3)]
    public void GetAiringsForEpisode_EstimatesNoUnlistedEpisodeWithoutOneConsistentOffset(
        int firstAnidb,
        int firstProvider,
        int secondAnidb,
        int secondProvider
    )
    {
        using var harness = new Harness();
        var crunchyroll = harness.Service.FindOrRegisterChannel("Crunchyroll", AiringChannelType.Streaming);
        harness.Schedule(
            harness.Primary,
            "crunchyroll",
            crunchyroll.ChannelID,
            [new AiringTrackData(AiringKind.Original, "ja")],
            (0, harness.Air(1, 14, 30)),
            (1, harness.Air(8, 14, 30))
        );
        var anidbEpisodes = harness.UseLinkedAnime(1, (firstAnidb, firstProvider), (secondAnidb, secondProvider));

        Assert.Empty(harness.Service.GetAiringsForEpisode(anidbEpisodes[3]));
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(3, true)]
    public void GetAiringsForEpisode_PlacesAnUnlistedEpisodeOnlyThroughTheSchedulesSeason(int firstLinked, bool estimated)
    {
        using var harness = new Harness();
        var tokyo = harness.Service.FindOrRegisterChannel("TOKYO MX", AiringChannelType.Television);
        var season = harness.ProviderSeason("s2", 2, 3);
        var schedule = harness.Service.AddOrUpdateSchedule(harness.Primary, harness.ScheduleData("mx", tokyo.ChannelID) with { Season = season });
        harness.Service.SetAirings(
            harness.Primary,
            schedule,
            [
                new EpisodeAiringData() { Episode = harness.Episodes[2], AiredAt = harness.Air(15, 23) },
                new EpisodeAiringData() { Episode = harness.Episodes[3], AiredAt = harness.Air(22, 23) },
            ]
        );
        var anidbEpisodes = harness.UseLinkedAnime(29, (1, firstLinked), (2, firstLinked + 1));

        var airings = harness.Service.GetAiringsForEpisode(anidbEpisodes[2]);

        Assert.Equal(estimated ? [harness.Air(43, 23)] : [], airings.Select(airing => airing.AiredAt).ToList());
    }

    [Fact]
    public void IsPreferred_FlagsTheEstimateOnThePreferredChannelOfAnEpisodeItsSourceDoesNotListYet()
    {
        using var harness = new Harness();
        var crunchyroll = harness.Service.FindOrRegisterChannel("Crunchyroll", AiringChannelType.Streaming);
        var bs11 = harness.Service.FindOrRegisterChannel("BS11", AiringChannelType.Television);
        harness.Schedule(
            harness.Primary,
            "crunchyroll",
            crunchyroll.ChannelID,
            [new AiringTrackData(AiringKind.Original, "ja")],
            (0, harness.Air(1, 14, 30)),
            (1, harness.Air(8, 14, 30))
        );
        var anidbEpisodes = harness.UseLinkedAnime(1, (1, 1), (2, 2));
        var anime = new Mock<ISeries>();
        anime.SetupGet(series => series.ID).Returns(new MetadataGuid(MetadataSource.AniDB, MetadataEntityType.Series, Harness.LinkedAnimeID.ToString()));
        anime.SetupGet(series => series.Source).Returns(MetadataSource.AniDB);
        anime.SetupGet(series => series.EntityType).Returns(MetadataEntityType.Series);
        var broadcast = harness.Service.AddOrUpdateSchedule(harness.Secondary, harness.ScheduleData("bs11", bs11.ChannelID, series: anime.Object));
        harness.Service.SetAirings(harness.Secondary, broadcast, [new EpisodeAiringData() { Episode = anidbEpisodes[2], AiredAt = harness.Air(20, 15) }]);

        var airings = harness.Service.GetAiringsForEpisode(
            anidbEpisodes[2],
            new EpisodeAiringFilteringOptions() { PreferredChannels = [crunchyroll.ChannelID] }
        );

        Assert.Equal(
            [(crunchyroll.ChannelID, true, true), (bs11.ChannelID, false, false)],
            airings.Select(airing => (airing.Channel!.ChannelID, airing.IsEstimated, airing.IsPreferred)).ToList()
        );
    }

    [Fact]
    public void GetAiringsForEpisode_PreferredOnlyGivesOneAiringPerEpisode()
    {
        using var harness = new Harness();
        var tokyo = harness.Service.FindOrRegisterChannel("TOKYO MX", AiringChannelType.Television);
        var bs11 = harness.Service.FindOrRegisterChannel("BS11", AiringChannelType.Television);
        harness.Schedule(harness.Primary, "mx", tokyo.ChannelID, [new AiringTrackData(AiringKind.Original, "ja")], (0, harness.Air(1, 23)));
        harness.Schedule(harness.Primary, "bs11", bs11.ChannelID, [new AiringTrackData(AiringKind.Original, "ja")], (0, harness.Air(1, 23, 30)));

        Assert.Single(harness.Service.GetAiringsForEpisode(harness.Episodes[0], new EpisodeAiringFilteringOptions() { PreferredOnly = true }));
    }

    [Fact]
    public void GetAiringsForEpisode_CollapsesOneChannelTwoProvidersSpellTheLanguageOfDifferently()
    {
        using var harness = new Harness();
        var crunchyroll = harness.Service.FindOrRegisterChannel("Crunchyroll", AiringChannelType.Streaming);
        harness.Schedule(harness.Primary, "first", crunchyroll.ChannelID, [new AiringTrackData(AiringKind.Subtitled, "en")], (0, harness.Air(1, 15)));
        harness.Schedule(harness.Secondary, "second", crunchyroll.ChannelID, [new AiringTrackData(AiringKind.Subtitled, "eng")], (0, harness.Air(1, 15)));

        // "en" and "eng" are the same language, so the two lines are one slot.
        var airing = Assert.Single(harness.Service.GetAiringsForEpisode(harness.Episodes[0], new EpisodeAiringFilteringOptions() { IncludeEstimates = false }));
        Assert.Equal(harness.Service.GetProviderInfo(harness.Primary).ID, airing.ProviderID);
    }

    [Fact]
    public void GetAiringsForEpisode_KeepsARepeatBroadcastOnOneChannel()
    {
        using var harness = new Harness();
        var aichi = harness.Service.FindOrRegisterChannel("テレビ愛知", AiringChannelType.Television);
        harness.Repeats(aichi.ChannelID, 0, harness.Air(1, 23), harness.Air(4, 2), harness.Air(6, 3));

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
            tokyo.ChannelID,
            [new AiringTrackData(AiringKind.Original, "ja")],
            (0, harness.Air(1, 23)),
            (1, harness.Air(8, 23)),
            (2, harness.Air(15, 23))
        );

        // Every airing on a schedule shares its channel and its tracks, so a
        // read that de-duplicated on those alone answered with one episode.
        var airings = harness.Service.GetAiringsForSchedule(schedule.ID, new EpisodeAiringFilteringOptions() { IncludeEstimates = false });

        Assert.Equal(3, airings.Count);
        Assert.Equal(3, airings.Select(airing => airing.EpisodeID).Distinct().Count());
    }

    [Fact]
    public void GetAiringsInRange_FindsARepeatBroadcastOnItsOwnDay()
    {
        using var harness = new Harness();
        var aichi = harness.Service.FindOrRegisterChannel("テレビ愛知", AiringChannelType.Television);
        harness.Repeats(aichi.ChannelID, 0, harness.Air(1, 23), harness.Air(4, 2), harness.Air(6, 3));

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
        harness.Schedule(harness.Primary, "tbs", tbs.ChannelID, [new AiringTrackData(AiringKind.Original, "ja")], (0, harness.Air(1, 23)));
        harness.Schedule(harness.Primary, "atx", atx.ChannelID, [new AiringTrackData(AiringKind.Original, "ja")], (0, harness.Air(3, 23)));

        // The episode's best airing overall is the earlier one on TBS, which is
        // outside this window; the day it actually airs on AT-X still has it.
        var airings = harness.Service.GetAiringsInRange(
            harness.Air(3, 0),
            harness.Air(3, 23, 59),
            new EpisodeAiringFilteringOptions() { IncludeEstimates = false, PreferredOnly = true }
        );

        var airing = Assert.Single(airings);
        Assert.Equal(atx.ChannelID, airing.Channel?.ChannelID);
    }

    [Fact]
    public void GetAiringsInRange_PreferredOnlyStillGivesOneAiringPerEpisode()
    {
        using var harness = new Harness();
        var tbs = harness.Service.FindOrRegisterChannel("TBS", AiringChannelType.Television);
        var atx = harness.Service.FindOrRegisterChannel("AT-X", AiringChannelType.Television);
        harness.Schedule(harness.Primary, "tbs", tbs.ChannelID, [new AiringTrackData(AiringKind.Original, "ja")], (0, harness.Air(3, 22)), (1, harness.Air(3, 20)));
        harness.Schedule(harness.Primary, "atx", atx.ChannelID, [new AiringTrackData(AiringKind.Original, "ja")], (0, harness.Air(3, 23)), (1, harness.Air(3, 21)));

        var airings = harness.Service.GetAiringsInRange(
            harness.Air(3, 0),
            harness.Air(3, 23, 59),
            new EpisodeAiringFilteringOptions() { IncludeEstimates = false, PreferredOnly = true }
        );

        // Two episodes air in the window, so the reduced read answers with two.
        Assert.Equal(2, airings.Count);
        Assert.Equal(2, airings.Select(airing => airing.EpisodeID).Distinct().Count());
        Assert.All(airings, airing => Assert.Equal(tbs.ChannelID, airing.Channel?.ChannelID));
    }

    [Fact]
    public void GetAiringByID_ResolvesAnEstimateTheSameWayItResolvesAStoredAiring()
    {
        using var harness = new Harness();
        var tokyo = harness.Service.FindOrRegisterChannel("TOKYO MX", AiringChannelType.Television);
        harness.Schedule(
            harness.Primary,
            "mx",
            tokyo.ChannelID,
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
        harness.Schedule(harness.Primary, "mx", tokyo.ChannelID, [new AiringTrackData(AiringKind.Original, "ja")], (0, harness.Air(1, 15, 30)));

        Assert.Null(harness.Service.GetAiringByID(Guid.NewGuid()));
    }

    [Fact]
    public void IsPreferred_FlagsOnlyThePreferredChannelOfAnEpisode()
    {
        using var harness = new Harness();
        var tokyo = harness.Service.FindOrRegisterChannel("TOKYO MX", AiringChannelType.Television);
        var bs11 = harness.Service.FindOrRegisterChannel("BS11", AiringChannelType.Television);
        harness.Schedule(harness.Primary, "mx", tokyo.ChannelID, [new AiringTrackData(AiringKind.Original, "ja")], (0, harness.Air(1, 23)));
        harness.Schedule(harness.Primary, "bs11", bs11.ChannelID, [new AiringTrackData(AiringKind.Original, "ja")], (0, harness.Air(1, 23, 30)));

        var airings = harness.Service.GetAiringsForEpisode(harness.Episodes[0], new EpisodeAiringFilteringOptions()
        {
            IncludeEstimates = false,
            PreferredChannels = [bs11.ChannelID],
        });

        Assert.Equal(
            [(bs11.ChannelID, true), (tokyo.ChannelID, false)],
            airings.Select(airing => (airing.Channel!.ChannelID, airing.IsPreferred)).ToList()
        );
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void IsPreferred_MatchesWhatPreferredOnlyKeepsInTheWindow(int fromDay)
    {
        using var harness = new Harness();
        var tbs = harness.Service.FindOrRegisterChannel("TBS", AiringChannelType.Television);
        var atx = harness.Service.FindOrRegisterChannel("AT-X", AiringChannelType.Television);
        harness.Schedule(harness.Primary, "tbs", tbs.ChannelID, [new AiringTrackData(AiringKind.Original, "ja")], (0, harness.Air(1, 23)), (1, harness.Air(3, 20)));
        harness.Schedule(harness.Primary, "atx", atx.ChannelID, [new AiringTrackData(AiringKind.Original, "ja")], (0, harness.Air(3, 23)), (1, harness.Air(3, 21)));
        var from = harness.Air(fromDay, 0);
        var to = harness.Air(3, 23, 59);

        var every = harness.Service.GetAiringsInRange(from, to, new EpisodeAiringFilteringOptions() { IncludeEstimates = false });
        var preferred = harness.Service.GetAiringsInRange(
            from,
            to,
            new EpisodeAiringFilteringOptions() { IncludeEstimates = false, PreferredOnly = true }
        );

        // From the 3rd on, the first episode's earlier TBS airing is outside the window, so its AT-X one is flagged.
        Assert.Equal(preferred.Select(airing => airing.ID).Order(), every.Where(airing => airing.IsPreferred).Select(airing => airing.ID).Order());
        Assert.Equal(2, preferred.Count);
        Assert.All(preferred, airing => Assert.True(airing.IsPreferred));
        Assert.Equal(fromDay is 3, preferred.Any(airing => airing.Channel?.ChannelID == atx.ChannelID));
    }

    [Fact]
    public void IsPreferred_ADateOnlyEntryAloneForItsEpisodeIsPreferred()
    {
        using var harness = new Harness();
        var anidbEpisode = harness.AnidbEpisode(new DateOnly(2026, 10, 4));

        var entry = Assert.Single(harness.Service.GetAiringsForEpisode(anidbEpisode, new EpisodeAiringFilteringOptions() { IncludeDateOnly = true }));

        Assert.True(entry.IsDateOnly);
        Assert.True(entry.IsPreferred);
    }

    #endregion

    #region Linked Entities

    [Fact]
    public void GetAiringsForEpisode_FollowsLinksForAShokoEpisodeByDefault()
    {
        using var harness = new Harness();
        var crunchyroll = harness.Service.FindOrRegisterChannel("Crunchyroll", AiringChannelType.Streaming);
        harness.Schedule(harness.Primary, "cr", crunchyroll.ChannelID, [new AiringTrackData(AiringKind.Subtitled, "en")], (0, harness.Air(1, 15)));

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
            crunchyroll.ChannelID,
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

        Assert.Equal(1, harness.Service.RunRetentionSweep(cancellationToken: TestContext.Current.CancellationToken));
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

        Assert.Equal(0, harness.Service.RunRetentionSweep(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(2, harness.Airings.Object.GetAll().Count);
    }

    [Fact]
    public void RetentionSweep_ReportsUpTo100_AndRemovesNothingOnceCancelled()
    {
        using var harness = new Harness();
        harness.Settings.AutoCleanup = false;
        var schedule = harness.Service.AddOrUpdateSchedule(harness.Primary, harness.ScheduleData());
        harness.Service.SetAirings(harness.Primary, schedule, [
            new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = DateTime.UtcNow.AddYears(-3) },
        ]);
        harness.Settings.AutoCleanup = true;
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var progress = new List<decimal>();

        Assert.Throws<OperationCanceledException>(() => harness.Service.RunRetentionSweep(cancellationToken: cancelled.Token));
        Assert.Single(harness.Schedules.Object.GetAll());
        Assert.Equal(1, harness.Service.RunRetentionSweep(new ListProgress(progress), TestContext.Current.CancellationToken));
        Assert.Equal([0m, 100m], progress);
    }

    /// <summary>
    /// Adds every value reported to a list, in order.
    /// </summary>
    /// <param name="values">The list.</param>
    private sealed class ListProgress(List<decimal> values) : IProgress<decimal>
    {
        public void Report(decimal value) => values.Add(value);
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

        Assert.Equal(0, harness.Service.RunRetentionSweep(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Single(harness.Schedules.Object.GetAll());
    }

    [Fact]
    public void RetentionSweep_LeavesAnEmptyScheduleAloneInsideItsBuffer()
    {
        using var harness = new Harness();
        harness.Service.AddOrUpdateSchedule(harness.Primary, harness.ScheduleData());

        Assert.Equal(0, harness.Service.RunRetentionSweep(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Single(harness.Schedules.Object.GetAll());
    }

    [Fact]
    public void RetentionSweep_TakesAnEmptySchedulePastItsBuffer()
    {
        using var harness = new Harness();
        harness.Service.AddOrUpdateSchedule(harness.Primary, harness.ScheduleData());
        var row = Assert.Single(harness.Schedules.Object.GetAll());
        row.CreatedAt = DateTime.UtcNow.AddHours(-2);

        Assert.Equal(1, harness.Service.RunRetentionSweep(cancellationToken: TestContext.Current.CancellationToken));
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
    public void RetentionCutoff_FollowsTheClampedWindowAndIsNullWhileCleanupIsOff()
    {
        using var harness = new Harness();
        harness.Settings.RetentionMonths = 1;

        var before = DateTime.UtcNow.AddMonths(-AiringScheduleServiceSettings.MinimumRetentionMonths);
        var cutoff = harness.Service.RetentionCutoff;
        var after = DateTime.UtcNow.AddMonths(-AiringScheduleServiceSettings.MinimumRetentionMonths);
        Assert.NotNull(cutoff);
        Assert.InRange(cutoff.Value, before, after);

        // The window itself stays while cleanup is off.
        harness.Settings.AutoCleanup = false;
        Assert.Null(harness.Service.RetentionCutoff);
        var retention = harness.Service.Retention;
        Assert.InRange(DateTime.UtcNow - retention, before.AddSeconds(-5), DateTime.UtcNow.AddMonths(-AiringScheduleServiceSettings.MinimumRetentionMonths).AddSeconds(5));
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
        Assert.Equal(1, harness.Service.RunRetentionSweep(cancellationToken: TestContext.Current.CancellationToken));
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
        Assert.Equal(0, harness.Service.RunRetentionSweep(cancellationToken: TestContext.Current.CancellationToken));
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
        Assert.Equal(0, harness.Service.RunRetentionSweep(cancellationToken: TestContext.Current.CancellationToken));

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
        Assert.Equal(0, harness.Service.RunRetentionSweep(cancellationToken: TestContext.Current.CancellationToken));
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
        Assert.Equal(0, harness.Service.RunRetentionSweep(cancellationToken: TestContext.Current.CancellationToken));

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
        var pulled = airings.Single(airing => airing.EpisodeID == harness.Episodes[1].ID);

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
        Assert.Equal(0, harness.Service.RunRetentionSweep(cancellationToken: TestContext.Current.CancellationToken));
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
        Assert.Equal(0, harness.Service.RunRetentionSweep(cancellationToken: TestContext.Current.CancellationToken));
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
        var recent = airings.Single(airing => airing.EpisodeID == harness.Episodes[1].ID);

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
        Assert.Equal(0, harness.Service.RunRetentionSweep(cancellationToken: TestContext.Current.CancellationToken));

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

        Assert.Equal(first.ChannelID, second.ChannelID);
        Assert.Equal(first.ChannelID, wide.ChannelID);
        Assert.NotEqual(first.ChannelID, streaming.ChannelID);
        // The spelling from the first registration is the one that is kept.
        Assert.Equal("TOKYO MX", second.Name);
    }

    [Fact]
    public void FindOrRegisterChannel_KeysTheCountry()
    {
        using var harness = new Harness();

        var japan = harness.Service.FindOrRegisterChannel("ABC", AiringChannelType.Television, "JP");
        var lowerCase = harness.Service.FindOrRegisterChannel(" abc ", AiringChannelType.Television, "jp");
        var unitedStates = harness.Service.FindOrRegisterChannel("ABC", AiringChannelType.Television, "US");
        var nowhere = harness.Service.FindOrRegisterChannel("ABC", AiringChannelType.Television);

        Assert.Equal(japan.ChannelID, lowerCase.ChannelID);
        Assert.Equal("JP", lowerCase.CountryCode);
        Assert.Equal("ABC", japan.Name);
        Assert.Equal(3, new[] { japan.ChannelID, unitedStates.ChannelID, nowhere.ChannelID }.Distinct().Count());
        Assert.Equal(IAiringScheduleService.GetChannelID("abc", AiringChannelType.Television, "JP"), japan.ChannelID);
        // A channel without a country keeps the ID it had before countries were keyed.
        Assert.Equal(UuidUtility.GetV5("ChannelType=Television,Name=abc", IAiringScheduleService.ChannelIdentifierNamespace), nowhere.ChannelID);
        Assert.Throws<ArgumentException>(() => harness.Service.FindOrRegisterChannel("ABC", AiringChannelType.Television, "JPN"));
    }

    [Fact]
    public void GetChannelByName_LooksWithinTheCountry()
    {
        using var harness = new Harness();
        var japan = harness.Service.FindOrRegisterChannel("ABC", AiringChannelType.Television, "JP");
        harness.Service.AddChannelAliases(japan, ["Asahi Broadcasting"]);

        Assert.Equal(japan.ChannelID, harness.Service.GetChannelByName("asahi broadcasting", AiringChannelType.Television, "JP")?.ChannelID);
        Assert.Null(harness.Service.GetChannelByName("ABC", AiringChannelType.Television));
        Assert.Null(harness.Service.GetChannelByName("Asahi Broadcasting", AiringChannelType.Television, "US"));
    }

    [Fact]
    public void FindOrRegisterChannel_FindsAnAliasBeforeRegistering()
    {
        using var harness = new Harness();
        var owner = harness.Service.FindOrRegisterChannel("Tokyo Metropolitan Television", AiringChannelType.Television, "JP");
        harness.Service.AddChannelAliases(owner, ["TOKYO MX"]);
        var registered = new List<IAiringChannel>();
        harness.Service.ChannelRegistered += (_, args) => registered.Add(args.Channel);

        var found = harness.Service.FindOrRegisterChannel("Tokyo MX", AiringChannelType.Television, "JP");

        Assert.Equal(owner.ChannelID, found.ChannelID);
        Assert.Empty(registered);
        // The alias stays where it was: registering never takes a name from another channel.
        Assert.Equal(["TOKYO MX"], harness.Service.GetChannelByID(owner.ChannelID)!.Aliases);
        // Another country is another registry, so the alias answers nothing there.
        var bare = harness.Service.FindOrRegisterChannel("TOKYO MX", AiringChannelType.Television);
        Assert.NotEqual(owner.ChannelID, bare.ChannelID);
        Assert.Equal(bare.ChannelID, Assert.Single(registered).ChannelID);
    }

    [Fact]
    public void AddChannelAliases_RefusesANameAnotherChannelAlreadyAnswersTo()
    {
        using var harness = new Harness();
        var owner = harness.Service.FindOrRegisterChannel("TOKYO MX", AiringChannelType.Television);
        var other = harness.Service.FindOrRegisterChannel("BS11", AiringChannelType.Television);

        var exception = Assert.Throws<ChannelAliasConflictException>(() => harness.Service.AddChannelAliases(other, ["tokyo mx"]));
        Assert.True(exception.HeldAsOwnName);
        Assert.Equal(owner.ChannelID, exception.ConflictingChannel.ChannelID);

        // An alias equal to the channel's own name is nothing to do, not a conflict.
        Assert.Empty(harness.Service.AddChannelAliases(other, ["BS11"]).Aliases);
    }

    [Fact]
    public void AddChannelAliases_OnlyConflictsWithinTheCountry()
    {
        using var harness = new Harness();
        harness.Service.FindOrRegisterChannel("ABC", AiringChannelType.Television, "JP");
        var other = harness.Service.FindOrRegisterChannel("Asahi", AiringChannelType.Television, "US");

        Assert.Equal(["ABC"], harness.Service.AddChannelAliases(other, ["ABC"]).Aliases);
    }

    [Fact]
    public void SetChannelAliases_ReplacesTheListOrChangesNothing()
    {
        using var harness = new Harness();
        harness.Service.FindOrRegisterChannel("BS11", AiringChannelType.Television, "JP");
        var channel = harness.Service.FindOrRegisterChannel("TOKYO MX", AiringChannelType.Television, "JP");
        harness.Service.AddChannelAliases(channel, ["MX", "Tokyo Metropolitan Television"]);

        var replaced = harness.Service.SetChannelAliases(channel, ["ＭＸテレビ", "mx", "MX", "TOKYO MX"]);

        Assert.Equal(["ＭＸテレビ", "mx"], replaced.Aliases);
        Assert.Throws<ChannelAliasConflictException>(() => harness.Service.SetChannelAliases(channel, ["Tokyo MX 1", "bs11"]));
        Assert.Equal(["ＭＸテレビ", "mx"], harness.Service.GetChannelByID(channel.ChannelID)!.Aliases);
        Assert.Empty(harness.Service.SetChannelAliases(channel, []).Aliases);
    }

    [Fact]
    public void MergeChannels_MovesTheSchedulesAndKeepsEveryID()
    {
        using var harness = new Harness();
        var target = harness.Service.FindOrRegisterChannel("TV Tokyo", AiringChannelType.Television, "JP");
        var source = harness.Service.FindOrRegisterChannel("テレビ東京", AiringChannelType.Television);
        harness.Service.AddChannelAliases(source, ["TX"]);
        var tracks = new[] { new AiringTrackData(AiringKind.Original, "ja") };
        var keyed = harness.Schedule(harness.Secondary, "tx", source.ChannelID, tracks, (0, harness.Air(1, 18)), (1, harness.Air(8, 18)));
        var keyless = harness.Service.AddOrUpdateSchedule(harness.Primary, harness.ScheduleData(channelID: source.ChannelID));
        harness.Service.SetAirings(harness.Primary, keyless, [new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = harness.Air(1, 18) }]);
        var airingIDs = harness.Read(keyed).Concat(harness.Read(keyless)).Select(airing => airing.ID).ToList();

        var merged = harness.Service.MergeChannels(target, [source, source]);

        Assert.Equal(target.ChannelID, merged.ChannelID);
        Assert.Equal("JP", merged.CountryCode);
        Assert.Equal(["テレビ東京", "TX"], merged.Aliases);
        Assert.Null(harness.Service.GetChannelByID(source.ChannelID));
        Assert.Equal(
            new[] { keyed.ID, keyless.ID }.Order(),
            harness.Service.GetSchedulesForChannel(target.ChannelID).Select(schedule => schedule.ID).Order()
        );
        Assert.Equal(airingIDs, harness.Read(keyed).Concat(harness.Read(keyless)).Select(airing => airing.ID).ToList());

        // A provider naming the merged channel is handed the target, and its
        // keyless schedule, derived on the old channel, is still the one it updates.
        var renamed = harness.Service.FindOrRegisterChannel("テレビ東京", AiringChannelType.Television, "JP");
        Assert.Equal(target.ChannelID, renamed.ChannelID);
        var rewritten = harness.Service.AddOrUpdateSchedule(harness.Primary, harness.ScheduleData(channelID: renamed.ChannelID));
        Assert.Equal(keyless.ID, rewritten.ID);
        Assert.Equal(2, harness.Service.GetSchedulesForChannel(target.ChannelID).Count);
    }

    [Fact]
    public void MergeChannels_FoldsTheChannelSettings()
    {
        using var harness = new Harness();
        var first = harness.Service.FindOrRegisterChannel("BS11", AiringChannelType.Television, "JP");
        var target = harness.Service.FindOrRegisterChannel("AT-X", AiringChannelType.Television, "JP");
        var source = harness.Service.FindOrRegisterChannel("AT-X", AiringChannelType.Television);
        var other = harness.Service.FindOrRegisterChannel("ＡＴ－Ｘ HD", AiringChannelType.Television, "JP");
        harness.Settings.PreferredChannels = [first.ChannelID, source.ChannelID, other.ChannelID, target.ChannelID];
        harness.Service.SetChannelHidden(source, true);
        harness.Service.SetChannelHidden(other, true);

        harness.Service.MergeChannels(target, [source, other]);

        Assert.Equal([first.ChannelID, target.ChannelID], harness.Settings.PreferredChannels);
        Assert.Empty(harness.Service.HiddenChannelIDs);
        Assert.False(harness.Service.GetChannelByID(target.ChannelID)!.IsHidden);
        // The source's own name is the target's, so only the other name is added.
        Assert.Equal(["ＡＴ－Ｘ HD"], harness.Service.GetChannelByID(target.ChannelID)!.Aliases);
    }

    [Fact]
    public void MergeChannels_RefusesAnotherTypeOrItself()
    {
        using var harness = new Harness();
        var target = harness.Service.FindOrRegisterChannel("ABEMA", AiringChannelType.Streaming, "JP");
        var television = harness.Service.FindOrRegisterChannel("ABEMA", AiringChannelType.Television, "JP");

        Assert.Throws<ArgumentException>(() => harness.Service.MergeChannels(target, [television]));
        Assert.Throws<ArgumentException>(() => harness.Service.MergeChannels(target, [target]));
        Assert.NotNull(harness.Service.GetChannelByID(television.ChannelID));
    }

    [Fact]
    public void AddOrUpdateSchedule_MovesAKeyedScheduleToItsChannelInAnotherCountry()
    {
        using var harness = new Harness();
        // Registered in the country first, or the bare channel would take it.
        var japan = harness.Service.FindOrRegisterChannel("BS11", AiringChannelType.Television, "JP");
        var bare = harness.Service.FindOrRegisterChannel("BS11", AiringChannelType.Television);
        harness.Service.AddChannelAliases(bare, ["BSイレブン"]);
        harness.Service.SetChannelHidden(bare, true);
        var tracks = new[] { new AiringTrackData(AiringKind.Original, "ja") };
        var schedule = harness.Schedule(harness.Primary, "128", bare.ChannelID, tracks, (0, harness.Air(1, 23)));
        var airingID = Assert.Single(harness.Read(schedule)).ID;

        var moved = harness.Service.AddOrUpdateSchedule(harness.Primary, harness.ScheduleData("128", japan.ChannelID));

        Assert.Equal(schedule.ID, moved.ID);
        Assert.Equal(japan.ChannelID, moved.Channel?.ChannelID);
        Assert.Equal(airingID, Assert.Single(harness.Read(moved)).ID);
        // Nothing airs on the bare channel any more, so it folded into the one
        // with the country, which keeps its aliases and its hidden state.
        Assert.Null(harness.Service.GetChannelByID(bare.ChannelID));
        Assert.Equal(["BSイレブン"], harness.Service.GetChannelByID(japan.ChannelID)!.Aliases);
        Assert.Equal([japan.ChannelID], harness.Service.HiddenChannelIDs);
    }

    [Fact]
    public void GetChannelByName_SkipsAliasesWhenAsked()
    {
        using var harness = new Harness();
        var owner = harness.Service.FindOrRegisterChannel("Tokyo Metropolitan Television", AiringChannelType.Television);
        harness.Service.AddChannelAliases(owner, ["TOKYO MX"]);

        Assert.Equal(owner.ChannelID, harness.Service.GetChannelByName("TOKYO MX", AiringChannelType.Television)?.ChannelID);
        Assert.Null(harness.Service.GetChannelByName("TOKYO MX", AiringChannelType.Television, useAliases: false));
    }

    #endregion

    #region Channel Countries

    [Theory]
    [InlineData("Tokyo MX (JP)", true, "Tokyo MX", "JP")]
    [InlineData("Amazon(US)", true, "Amazon", "US")]
    [InlineData("AT-X", false, "AT-X", "")]
    [InlineData("Crunchyroll (Sub)", false, "Crunchyroll (Sub)", "")]
    [InlineData("Amazon (us)", false, "Amazon (us)", "")]
    public void TrySplitRegionalChannelName_ReadsATrailingCountry(string name, bool expected, string brand, string countryCode)
    {
        Assert.Equal(expected, DatabaseFixes.TrySplitRegionalChannelName(name, out var actualBrand, out var actualCountryCode));
        Assert.Equal((brand, countryCode), (actualBrand, actualCountryCode));
    }

    [Fact]
    public void KeyAiringChannelsByCountry_MovesTheCountryOutOfTheNameAndMergesTheDuplicates()
    {
        using var harness = new Harness();
        var tracks = new[] { new AiringTrackData(AiringKind.Original, "ja") };
        var syoboiMx = harness.Service.FindOrRegisterChannel("TOKYO MX", AiringChannelType.Television);
        harness.Service.AddChannelAliases(syoboiMx, ["ＭＸテレビ"]);
        var tvmazeMx = harness.Service.FindOrRegisterChannel("Tokyo MX (JP)", AiringChannelType.Television);
        var netflix = harness.Service.FindOrRegisterChannel("Netflix", AiringChannelType.Streaming);
        var netflixJapan = harness.Service.FindOrRegisterChannel("Netflix (JP)", AiringChannelType.Streaming);
        var huluJapan = harness.Service.FindOrRegisterChannel("Hulu (JP)", AiringChannelType.Streaming);
        var hulu = harness.Service.FindOrRegisterChannel("Hulu", AiringChannelType.Streaming);
        var abcJapan = harness.Service.FindOrRegisterChannel("ABC (JP)", AiringChannelType.Television);
        var abcUnitedStates = harness.Service.FindOrRegisterChannel("ABC (US)", AiringChannelType.Television);
        var bs11 = harness.Service.FindOrRegisterChannel("BS11", AiringChannelType.Television);
        var syoboiSchedule = harness.Schedule(harness.Primary, "19", syoboiMx.ChannelID, tracks, (0, harness.Air(1, 23)));
        var tvmazeSchedule = harness.Schedule(harness.Secondary, "tvmaze", tvmazeMx.ChannelID, tracks, (0, harness.Air(1, 23)));
        var netflixSchedule = harness.Schedule(harness.Primary, "nf", netflixJapan.ChannelID, tracks, (0, harness.Air(1, 15)));
        var airingIDs = new[] { syoboiSchedule, tvmazeSchedule, netflixSchedule }.SelectMany(harness.Read).Select(airing => airing.ID).ToList();
        // The views read their row, so the IDs from before are kept aside.
        var (huluJapanID, abcUnitedStatesID) = (huluJapan.ChannelID, abcUnitedStates.ChannelID);
        harness.Settings.PreferredChannels = [tvmazeMx.ChannelID, netflixJapan.ChannelID, syoboiMx.ChannelID];
        harness.Service.SetChannelHidden(abcJapan, true);

        DatabaseFixes.KeyAiringChannelsByCountry(harness.ConfigurationProvider, NullLogger.Instance);

        var channels = harness.Service.GetAllChannels().Select(channel => (channel.Name, channel.Type, channel.CountryCode)).ToList();
        Assert.Equal(
            [
                ("ABC", AiringChannelType.Television, "JP"),
                ("ABC", AiringChannelType.Television, "US"),
                ("BS11", AiringChannelType.Television, null),
                ("TOKYO MX", AiringChannelType.Television, "JP"),
                ("Hulu", AiringChannelType.Streaming, null),
                ("Hulu", AiringChannelType.Streaming, "JP"),
                ("Netflix", AiringChannelType.Streaming, null),
            ],
            channels
        );
        var mx = harness.Service.GetChannelByName("Tokyo MX", AiringChannelType.Television, "JP")!;
        var abc = harness.Service.GetChannelByName("ABC", AiringChannelType.Television, "JP")!;
        Assert.Equal(["ＭＸテレビ"], mx.Aliases);
        Assert.Equal(IAiringScheduleService.GetChannelID("TOKYO MX", AiringChannelType.Television, "JP"), mx.ChannelID);
        // The channels already keyed right keep their IDs.
        Assert.Equal(netflix.ChannelID, harness.Service.GetChannelByName("Netflix", AiringChannelType.Streaming)?.ChannelID);
        Assert.Equal(hulu.ChannelID, harness.Service.GetChannelByName("Hulu", AiringChannelType.Streaming)?.ChannelID);
        Assert.Equal(bs11.ChannelID, harness.Service.GetChannelByName("BS11", AiringChannelType.Television)?.ChannelID);
        Assert.NotEqual(huluJapanID, harness.Service.GetChannelByName("Hulu", AiringChannelType.Streaming, "JP")?.ChannelID);

        // The schedules follow their channels, and no schedule or airing ID moves.
        Assert.Equal(mx.ChannelID, harness.Service.GetScheduleByID(syoboiSchedule.ID)?.Channel?.ChannelID);
        Assert.Equal(mx.ChannelID, harness.Service.GetScheduleByID(tvmazeSchedule.ID)?.Channel?.ChannelID);
        Assert.Equal(netflix.ChannelID, harness.Service.GetScheduleByID(netflixSchedule.ID)?.Channel?.ChannelID);
        Assert.Equal(airingIDs, new[] { syoboiSchedule, tvmazeSchedule, netflixSchedule }.SelectMany(harness.Read).Select(airing => airing.ID).ToList());

        // And so do the settings.
        Assert.Equal([mx.ChannelID, netflix.ChannelID], harness.Settings.PreferredChannels);
        Assert.Equal([abc.ChannelID], harness.Service.HiddenChannelIDs);
        Assert.NotEqual(abcUnitedStatesID, harness.Service.GetChannelByName("ABC", AiringChannelType.Television, "US")?.ChannelID);
    }

    [Fact]
    public void MergeChannels_GivesTheCountryAndARegistrationThereLandsOnIt()
    {
        using var harness = new Harness();
        var bare = harness.Service.FindOrRegisterChannel("BS11イレブン", AiringChannelType.Television);
        var japan = harness.Service.FindOrRegisterChannel("BS11", AiringChannelType.Television, "JP");
        var tracks = new[] { new AiringTrackData(AiringKind.Original, "ja") };
        var schedule = harness.Schedule(harness.Primary, "211", bare.ChannelID, tracks, (0, harness.Air(1, 23)));

        var merged = harness.Service.MergeChannels(bare, [japan]);

        Assert.Equal(("BS11イレブン", "JP"), (merged.Name, merged.CountryCode));
        Assert.Equal(IAiringScheduleService.GetChannelID("BS11イレブン", AiringChannelType.Television, "JP"), merged.ChannelID);
        Assert.Equal(merged.ChannelID, harness.Service.FindOrRegisterChannel("BS11", AiringChannelType.Television, "JP").ChannelID);
        Assert.Equal(merged.ChannelID, harness.Service.GetScheduleByID(schedule.ID)?.Channel?.ChannelID);
        Assert.Single(harness.Service.GetAllChannels());
    }

    [Fact]
    public void FindOrRegisterChannel_FallsBackToAChannelWithoutACountry()
    {
        using var harness = new Harness();
        var eleven = harness.Service.FindOrRegisterChannel("BS Eleven", AiringChannelType.Television);
        harness.Service.AddChannelAliases(eleven, ["BS11"]);
        var netflix = harness.Service.FindOrRegisterChannel("Netflix", AiringChannelType.Streaming);

        Assert.Equal(eleven.ChannelID, harness.Service.GetChannelByName("BS11", AiringChannelType.Television, "JP")?.ChannelID);
        Assert.Null(harness.Service.GetChannelByName("BS11", AiringChannelType.Television, "JP", useAliases: false));
        Assert.Null(eleven.CountryCode);

        // Only stored data breaking one name, one answer ties an own name with an alias.
        var own = new AiringChannel("BS11", AiringChannelType.Television);
        harness.Channels.Object.Save(own);
        var found = harness.Service.FindOrRegisterChannel("bs11", AiringChannelType.Television, "JP");

        Assert.Equal(own.ChannelID, found.ChannelID);
        Assert.Equal(("BS11", "JP"), (found.Name, found.CountryCode));
        Assert.Null(eleven.CountryCode);
        // A streaming service without a country is global, so it keeps having none.
        Assert.Equal(netflix.ChannelID, harness.Service.FindOrRegisterChannel("Netflix", AiringChannelType.Streaming, "JP").ChannelID);
        Assert.Null(netflix.CountryCode);
    }

    [Fact]
    public void FindOrRegisterChannel_RegistersWhenTheChannelWithoutACountryIsUnclear()
    {
        using var harness = new Harness();
        harness.Channels.Object.Save(new AiringChannel("BS Eleven", AiringChannelType.Television) { Aliases = ["BS11"] });
        harness.Channels.Object.Save(new AiringChannel("BS 11ch", AiringChannelType.Television) { Aliases = ["BS11"] });
        // A namesake in another country may make it that country's station.
        harness.Service.FindOrRegisterChannel("ABC", AiringChannelType.Television, "US");
        harness.Service.FindOrRegisterChannel("ABC", AiringChannelType.Television);

        Assert.Null(harness.Service.GetChannelByName("BS11", AiringChannelType.Television, "JP"));
        Assert.Equal("JP", harness.Service.FindOrRegisterChannel("BS11", AiringChannelType.Television, "JP").CountryCode);
        Assert.Equal("JP", harness.Service.FindOrRegisterChannel("ABC", AiringChannelType.Television, "JP").CountryCode);
        Assert.Equal(6, harness.Service.GetAllChannels().Count);
    }

    [Fact]
    public void MergeChannels_GivesTheCountryOnlyWhenTheSourcesAgree()
    {
        using var harness = new Harness();
        var tokyo = harness.Service.FindOrRegisterChannel("テレビ東京", AiringChannelType.Television);
        var abc = harness.Service.FindOrRegisterChannel("朝日放送", AiringChannelType.Television);
        var netflix = harness.Service.FindOrRegisterChannel("Netflix", AiringChannelType.Streaming);

        var agreed = harness.Service.MergeChannels(
            tokyo,
            [
                harness.Service.FindOrRegisterChannel("TV Tokyo", AiringChannelType.Television, "JP"),
                harness.Service.FindOrRegisterChannel("TX", AiringChannelType.Television),
            ]
        );
        var disagreed = harness.Service.MergeChannels(
            abc,
            [
                harness.Service.FindOrRegisterChannel("ABC", AiringChannelType.Television, "JP"),
                harness.Service.FindOrRegisterChannel("ABC", AiringChannelType.Television, "US"),
            ]
        );
        var global = harness.Service.MergeChannels(netflix, [harness.Service.FindOrRegisterChannel("Netflix Japan", AiringChannelType.Streaming, "JP")]);

        Assert.Equal("JP", agreed.CountryCode);
        Assert.Null(disagreed.CountryCode);
        Assert.Null(global.CountryCode);
    }

    #endregion

    #region Channel Events

    [Fact]
    public void ChannelEvents_CarryTheAliasChangeAndTheActor()
    {
        using var harness = new Harness();
        var events = RecordChannelEvents(harness);
        var token = ActorContextTests.Token();

        var channel = harness.Service.FindOrRegisterChannel("TOKYO MX", AiringChannelType.Television, "JP");
        using (ActorContext.Begin(token))
        {
            harness.Service.AddChannelAliases(channel, ["MX"]);
            harness.Service.RemoveChannelAliases(channel, ["MX"]);
            harness.Service.SetChannelAliases(channel, ["Tokyo Metropolitan Television"]);
        }

        Assert.Equal(
            [
                (UpdateReason.Added, AiringChannelChangeKind.Registered),
                (UpdateReason.Updated, AiringChannelChangeKind.AliasesAdded),
                (UpdateReason.Updated, AiringChannelChangeKind.AliasesRemoved),
                (UpdateReason.Updated, AiringChannelChangeKind.AliasesSet),
            ],
            events.Select(args => (args.Reason, args.Kind))
        );
        Assert.Null(events[0].PreviousAliases);
        Assert.Null(events[0].Actor);
        Assert.Empty(events[1].PreviousAliases!);
        Assert.Equal(["MX"], events[2].PreviousAliases);
        Assert.Empty(events[3].PreviousAliases!);
        Assert.All(events.Skip(1), args => Assert.Same(token, args.Actor));
        Assert.All(events, args => Assert.Empty(args.MergedChannels));
    }

    [Fact]
    public void MergeChannels_RaisesTheSourcesAsMergedAwayAndTheTargetWithWhatTheyGaveIt()
    {
        using var harness = new Harness();
        var target = harness.Service.FindOrRegisterChannel("TV Tokyo", AiringChannelType.Television, "JP");
        harness.Service.AddChannelAliases(target, ["TX"]);
        var source = harness.Service.FindOrRegisterChannel("テレビ東京", AiringChannelType.Television);
        harness.Service.AddChannelAliases(source, ["tx", "TV Tokyo Corporation"]);
        var sourceID = source.ChannelID;
        var events = RecordChannelEvents(harness);
        var token = ActorContextTests.Token();

        using (ActorContext.Begin(token))
            harness.Service.MergeChannels(target, [source]);

        Assert.Equal(
            [
                (UpdateReason.Removed, AiringChannelChangeKind.MergedAway, sourceID),
                (UpdateReason.Updated, AiringChannelChangeKind.Merged, target.ChannelID),
            ],
            events.Select(args => (args.Reason, args.Kind, args.Channel.ChannelID))
        );
        Assert.Equal(target.ChannelID, events[0].TargetChannelID);
        var merged = events[1];
        Assert.Null(merged.PreviousChannelID);
        Assert.Equal(["TX"], merged.PreviousAliases);
        Assert.Equal([(sourceID, "テレビ東京")], merged.MergedChannels.Select(channel => (channel.ChannelID, channel.Name)));
        Assert.Equal(["テレビ東京", "TV Tokyo Corporation"], merged.AddedAliases);
        Assert.All(events, args => Assert.Same(token, args.Actor));
    }

    [Fact]
    public void MergeChannels_TakingACountryLinksTheTargetsOldID()
    {
        using var harness = new Harness();
        var target = harness.Service.FindOrRegisterChannel("テレビ東京", AiringChannelType.Television);
        var source = harness.Service.FindOrRegisterChannel("TV Tokyo", AiringChannelType.Television, "JP");
        var (oldID, sourceID) = (target.ChannelID, source.ChannelID);
        var events = RecordChannelEvents(harness);

        var merged = harness.Service.MergeChannels(target, [source]);

        Assert.Equal(
            [
                (UpdateReason.Removed, AiringChannelChangeKind.MergedAway, sourceID),
                (UpdateReason.Removed, AiringChannelChangeKind.MergedAway, oldID),
                (UpdateReason.Added, AiringChannelChangeKind.Merged, merged.ChannelID),
            ],
            events.Select(args => (args.Reason, args.Kind, args.Channel.ChannelID))
        );
        Assert.All(events.Take(2), args => Assert.Equal(merged.ChannelID, args.TargetChannelID));
        Assert.Equal(oldID, events[2].PreviousChannelID);
        Assert.Equal([sourceID], events[2].MergedChannels.Select(channel => channel.ChannelID));
        Assert.Equal(["TV Tokyo"], events[2].AddedAliases);
    }

    [Fact]
    public void FindOrRegisterChannel_RaisesTheCountryTakenWithTheOldID()
    {
        using var harness = new Harness();
        var bare = harness.Service.FindOrRegisterChannel("BS11", AiringChannelType.Television);
        harness.Service.AddChannelAliases(bare, ["BSイレブン"]);
        harness.Service.SetChannelHidden(bare, true);
        var oldID = bare.ChannelID;
        var events = RecordChannelEvents(harness);

        var found = harness.Service.FindOrRegisterChannel("BS11", AiringChannelType.Television, "JP");

        // The re-keyed channel keeps its hidden state.
        Assert.True(found.IsHidden);
        Assert.Equal([found.ChannelID], harness.Service.HiddenChannelIDs);

        Assert.Equal(
            [
                (UpdateReason.Removed, AiringChannelChangeKind.CountryTaken, oldID),
                (UpdateReason.Added, AiringChannelChangeKind.CountryTaken, found.ChannelID),
            ],
            events.Select(args => (args.Reason, args.Kind, args.Channel.ChannelID))
        );
        Assert.Equal(found.ChannelID, events[0].TargetChannelID);
        Assert.Equal(oldID, events[1].PreviousChannelID);
        Assert.Equal(["BSイレブン"], events[1].PreviousAliases);
        Assert.Empty(events[1].MergedChannels);
        Assert.Empty(events[1].AddedAliases);
    }

    [Fact]
    public void FindOrRegisterChannel_RaisesTheCountryTakenAsAMergeIntoTheChannelThere()
    {
        using var harness = new Harness();
        var holder = harness.Service.FindOrRegisterChannel("BSイレブン", AiringChannelType.Television, "JP");
        harness.Service.AddChannelAliases(holder, ["BS Eleven"]);
        var bare = harness.Service.FindOrRegisterChannel("BS Eleven", AiringChannelType.Television);
        harness.Service.AddChannelAliases(bare, ["BS11"]);
        var bareID = bare.ChannelID;
        var events = RecordChannelEvents(harness);

        var found = harness.Service.FindOrRegisterChannel("BS11", AiringChannelType.Television, "JP");

        Assert.Equal(holder.ChannelID, found.ChannelID);
        Assert.Equal(
            [
                (UpdateReason.Removed, AiringChannelChangeKind.CountryTaken, bareID),
                (UpdateReason.Updated, AiringChannelChangeKind.CountryTaken, holder.ChannelID),
            ],
            events.Select(args => (args.Reason, args.Kind, args.Channel.ChannelID))
        );
        Assert.Equal(holder.ChannelID, events[0].TargetChannelID);
        Assert.Null(events[1].PreviousChannelID);
        Assert.Equal(["BS Eleven"], events[1].PreviousAliases);
        Assert.Equal([bareID], events[1].MergedChannels.Select(channel => channel.ChannelID));
        Assert.Equal(["BS11"], events[1].AddedAliases);
    }

    [Fact]
    public void AddOrUpdateSchedule_RaisesTheCountryMovedForTheChannelLeft()
    {
        using var harness = new Harness();
        var japan = harness.Service.FindOrRegisterChannel("BS11", AiringChannelType.Television, "JP");
        var bare = harness.Service.FindOrRegisterChannel("BS11", AiringChannelType.Television);
        harness.Service.AddChannelAliases(bare, ["BSイレブン"]);
        harness.Service.SetChannelHidden(bare, true);
        var bareID = bare.ChannelID;
        var tracks = new[] { new AiringTrackData(AiringKind.Original, "ja") };
        harness.Schedule(harness.Primary, "128", bareID, tracks, (0, harness.Air(1, 23)));
        var events = RecordChannelEvents(harness);

        harness.Service.AddOrUpdateSchedule(harness.Primary, harness.ScheduleData("128", japan.ChannelID));

        Assert.Equal(
            [
                (UpdateReason.Removed, AiringChannelChangeKind.CountryMoved, bareID),
                (UpdateReason.Updated, AiringChannelChangeKind.CountryMoved, japan.ChannelID),
            ],
            events.Select(args => (args.Reason, args.Kind, args.Channel.ChannelID))
        );
        Assert.Equal(japan.ChannelID, events[0].TargetChannelID);
        Assert.Empty(events[1].PreviousAliases!);
        Assert.Equal([bareID], events[1].MergedChannels.Select(channel => channel.ChannelID));
        Assert.Equal(["BSイレブン"], events[1].AddedAliases);
        // The channel left was hidden, so the one moved to now is.
        Assert.False(events[1].PreviousIsHidden);
        Assert.True(events[1].Channel.IsHidden);
    }

    [Fact]
    public void SetChannelHidden_RaisesTheChangeOnceWithThePreviousStateAndTheActor()
    {
        using var harness = new Harness();
        var channel = harness.Service.FindOrRegisterChannel("BS11", AiringChannelType.Television, "JP");
        var events = RecordChannelEvents(harness);
        var token = ActorContextTests.Token();

        using (ActorContext.Begin(token))
        {
            harness.Service.SetChannelHidden(channel, true);
            harness.Service.SetChannelHidden(channel, true);
            harness.Service.SetChannelHidden(channel, false);
        }

        Assert.Equal(
            [
                (UpdateReason.Updated, AiringChannelChangeKind.HiddenChanged, false),
                (UpdateReason.Updated, AiringChannelChangeKind.HiddenChanged, true),
            ],
            events.Select(args => (args.Reason, args.Kind, args.PreviousIsHidden))
        );
        Assert.All(events, args => Assert.Same(token, args.Actor));
        Assert.Empty(harness.Service.HiddenChannelIDs);
    }

    /// <summary>
    /// Records the channel events the service raises from now on.
    /// </summary>
    /// <param name="harness">The harness whose service to listen to.</param>
    /// <returns>The events, in the order raised.</returns>
    private static List<AiringChannelEventArgs> RecordChannelEvents(Harness harness)
    {
        var events = new List<AiringChannelEventArgs>();
        harness.Service.ChannelRegistered += (_, args) => events.Add(args);
        return events;
    }

    #endregion

    #region Airing Notifications

    [Fact]
    public void Tick_DispatchesAnAiringInTheHorizonOnceAndOnlyOnce()
    {
        using var harness = new Harness();
        var now = Harness.Minute(DateTime.UtcNow);
        var tokyo = harness.Service.FindOrRegisterChannel("TOKYO MX", AiringChannelType.Television);
        harness.Schedule(harness.Primary, "mx", tokyo.ChannelID, [new AiringTrackData(AiringKind.Original, "ja")], (0, now.AddMinutes(5)));
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
        harness.Schedule(harness.Primary, "aichi", tokyo.ChannelID, tracks, (0, now.AddMinutes(5)));
        harness.Schedule(harness.Primary, "tx", tvTokyo.ChannelID, tracks, (0, now.AddMinutes(5)));
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
        Assert.Equal(new HashSet<Guid> { tokyo.ChannelID, tvTokyo.ChannelID }, dispatch.Airings.Select(airing => airing.Channel!.ChannelID).ToHashSet());
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
        Assert.Equal(tokyo.ChannelID, dispatch.Airings[0].Channel!.ChannelID);
    }

    [Fact]
    public void Tick_SkipsForwardInsteadOfReplayingWhatItMissed()
    {
        using var harness = new Harness();
        var now = Harness.Minute(DateTime.UtcNow);
        harness.SeedWatermark(now.AddHours(-3));
        var tokyo = harness.Service.FindOrRegisterChannel("TOKYO MX", AiringChannelType.Television);
        harness.Schedule(harness.Primary, "mx", tokyo.ChannelID, [new AiringTrackData(AiringKind.Original, "ja")], (0, now.AddMinutes(-30)));
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
        var schedule = harness.Schedule(harness.Primary, "mx", tokyo.ChannelID, [new AiringTrackData(AiringKind.Original, "ja")], (0, now.AddMinutes(10)));
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
        harness.Schedule(harness.Primary, "mx", tokyo.ChannelID, [new AiringTrackData(AiringKind.Original, "ja")], (0, now.AddMinutes(5)));

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
        harness.Schedule(harness.Primary, "mx", tokyo.ChannelID, [new AiringTrackData(AiringKind.Original, "ja")], (0, now.AddMinutes(5)));

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
        harness.Schedule(harness.Primary, "mx", tokyo.ChannelID, tracks, (0, now.AddMinutes(5)));
        harness.Schedule(harness.Primary, "bs11", bs11.ChannelID, tracks, (0, now.AddMinutes(5)));
        var tokyoOnly = new List<EpisodeAiredEventArgs>();
        var everything = new List<EpisodeAiredEventArgs>();
        using var first = harness.Service.SubscribeToAirings(
            tokyoOnly.Add,
            new EpisodeAiringFilteringOptions() { ChannelIDs = new HashSet<Guid> { tokyo.ChannelID } }
        );
        using var second = harness.Service.SubscribeToAirings(everything.Add);

        harness.Notifications.Tick(now);
        harness.Notifications.Tick(now.AddMinutes(5));

        // One horizon, two answers: the filters are the subscriber's, not the
        // ticker's.
        Assert.Equal(tokyo.ChannelID, Assert.Single(Assert.Single(tokyoOnly).Airings).Channel!.ChannelID);
        Assert.Equal(2, Assert.Single(everything).Airings.Count);
    }

    [Fact]
    public void SubscribeToAirings_StopsDispatchingOnceDisposed()
    {
        using var harness = new Harness();
        var now = Harness.Minute(DateTime.UtcNow);
        var tokyo = harness.Service.FindOrRegisterChannel("TOKYO MX", AiringChannelType.Television);
        var tracks = new[] { new AiringTrackData(AiringKind.Original, "ja") };
        harness.Schedule(harness.Primary, "mx", tokyo.ChannelID, tracks, (0, now.AddMinutes(5)), (1, now.AddMinutes(10)));
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
        harness.Schedule(harness.Primary, "mx", tokyo.ChannelID, [new AiringTrackData(AiringKind.Original, "ja")], (0, now.AddMinutes(5)));
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
        harness.OrphanSchedule(harness.Primary, "orphan", tokyo.ChannelID, harness.Air(1));

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
        harness.OrphanSchedule(harness.Primary, "orphan", tokyo.ChannelID, harness.Air(1));

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
        harness.Schedule(harness.Primary, "mx", tokyo.ChannelID, [new AiringTrackData(AiringKind.Original, "ja")], (0, harness.Air(1)));

        // The provider stored the airing against its own episode; asking from
        // the shoko side still reaches it, and every airing carries the shoko
        // episode the read was anchored to.
        var airings = harness.Service.GetAiringsForEpisode(harness.ShokoEpisodes[0], new EpisodeAiringFilteringOptions() { IncludeEstimates = false });
        Assert.Equal(harness.ShokoEpisodes[0].LocalID, Assert.Single(airings).ShokoEpisode?.LocalID);
    }

    [Fact]
    public void GetAiringsInRange_AutoFallsBackToRawWithNoEntityToInferFrom()
    {
        using var harness = new Harness();
        var tokyo = harness.Service.FindOrRegisterChannel("TOKYO MX", AiringChannelType.Television);
        var airedAt = harness.Air(1);
        harness.OrphanSchedule(harness.Primary, "orphan", tokyo.ChannelID, airedAt);
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
        harness.OrphanSchedule(harness.Primary, "orphan", tokyo.ChannelID, harness.Air(1));

        Assert.Single(harness.Service.GetSchedulesForSeries(harness.OrphanSeries));
        Assert.Empty(harness.Service.GetSchedulesForSeries(harness.OrphanSeries, new AiringScheduleFilteringOptions()
        {
            EntityAnchor = AiringEntityAnchor.Shoko,
        }));
    }

    #endregion

    #region Queries

    [Fact]
    public void GetAiringsInRange_ProviderIDsKeepOnlyTheGivenProviders()
    {
        using var harness = new Harness();
        var tbs = harness.Service.FindOrRegisterChannel("TBS", AiringChannelType.Television);
        var atx = harness.Service.FindOrRegisterChannel("AT-X", AiringChannelType.Television);
        harness.Schedule(harness.Primary, "tbs", tbs.ChannelID, [new AiringTrackData(AiringKind.Original, "ja")], (0, harness.Air(3, 22)));
        harness.Schedule(harness.Secondary, "atx", atx.ChannelID, [new AiringTrackData(AiringKind.Original, "ja")], (1, harness.Air(3, 23)));
        var secondaryID = harness.Service.GetProviderInfo(harness.Secondary).ID;

        var airings = harness.Service.GetAiringsInRange(
            harness.Air(3, 0),
            harness.Air(4, 0),
            new EpisodeAiringFilteringOptions() { IncludeEstimates = false, ProviderIDs = new HashSet<Guid> { secondaryID } }
        );

        Assert.Equal(secondaryID, Assert.Single(airings).ProviderID);
    }

    [Fact]
    public void GetAiringsInRange_ComparesInstantsInAnyOffsetWithTheEndExclusive()
    {
        using var harness = new Harness();
        var tbs = harness.Service.FindOrRegisterChannel("TBS", AiringChannelType.Television);
        var airedAt = harness.Air(3, 15);
        harness.Schedule(harness.Primary, "tbs", tbs.ChannelID, [new AiringTrackData(AiringKind.Original, "ja")], (0, airedAt));
        var options = new EpisodeAiringFilteringOptions() { IncludeEstimates = false };
        var tokyo = TimeSpan.FromHours(9);

        // The same instant written in +09:00 starts the range, and the end is exclusive.
        Assert.Single(harness.Service.GetAiringsInRange(new DateTimeOffset(airedAt).ToOffset(tokyo), new DateTimeOffset(airedAt.AddHours(1)), options));
        Assert.Empty(harness.Service.GetAiringsInRange(new DateTimeOffset(airedAt.AddHours(-1)).ToOffset(tokyo), new DateTimeOffset(airedAt), options));
    }

    [Fact]
    public void NextOnly_PerSeriesKeepsTheNextEpisodeOnTheReadersPreferredChannel()
    {
        using var harness = new Harness();
        var tbs = harness.Service.FindOrRegisterChannel("TBS", AiringChannelType.Television);
        var atx = harness.Service.FindOrRegisterChannel("AT-X", AiringChannelType.Television);
        harness.Schedule(
            harness.Primary,
            "tbs",
            tbs.ChannelID,
            [new AiringTrackData(AiringKind.Original, "ja")],
            (0, harness.Air(3, 22)),
            (1, harness.Air(10, 22))
        );
        harness.Schedule(
            harness.Primary,
            "atx",
            atx.ChannelID,
            [new AiringTrackData(AiringKind.Original, "ja")],
            (0, harness.Air(3, 23)),
            (1, harness.Air(10, 23))
        );

        // The second episode is next, and AT-X shows it even though TBS airs it first.
        var airing = Assert.Single(harness.Service.GetAiringsInRange(
            harness.Air(5, 0),
            harness.Air(20, 0),
            new EpisodeAiringFilteringOptions() { IncludeEstimates = false, NextOnly = true, PreferredChannels = [atx.ChannelID] }
        ));

        Assert.Equal((harness.Episodes[1].ID, atx.ChannelID), (airing.EpisodeID, airing.Channel?.ChannelID));
    }

    [Fact]
    public void NextOnly_PerChannelKeepsTheNextAiringOfEachChannel()
    {
        using var harness = new Harness();
        var tbs = harness.Service.FindOrRegisterChannel("TBS", AiringChannelType.Television);
        var atx = harness.Service.FindOrRegisterChannel("AT-X", AiringChannelType.Television);
        harness.Schedule(
            harness.Primary,
            "tbs",
            tbs.ChannelID,
            [new AiringTrackData(AiringKind.Original, "ja")],
            (0, harness.Air(3, 22)),
            (1, harness.Air(10, 22))
        );
        harness.Schedule(
            harness.Primary,
            "atx",
            atx.ChannelID,
            [new AiringTrackData(AiringKind.Original, "ja")],
            (0, harness.Air(4, 1)),
            (1, harness.Air(11, 1))
        );

        var airings = harness.Service.GetAiringsInRange(
            harness.Air(3, 23),
            harness.Air(20, 0),
            new EpisodeAiringFilteringOptions()
            {
                IncludeEstimates = false,
                NextOnly = true,
                NextPer = new HashSet<AiringNextGrouping> { AiringNextGrouping.Channel },
            }
        );

        // TBS premiered the first episode before the range, so AT-X's late showing of it is not next.
        Assert.Equal(
            [(harness.Episodes[1].ID, tbs.ChannelID), (harness.Episodes[1].ID, atx.ChannelID)],
            airings.Select(airing => (airing.EpisodeID, airing.Channel!.ChannelID)).ToList()
        );
    }

    [Fact]
    public void NextOnly_PerSeriesAndKindKeepsTheNextAiringOfEachKind()
    {
        using var harness = new Harness();
        var tbs = harness.Service.FindOrRegisterChannel("TBS", AiringChannelType.Television);
        var stream = harness.Service.FindOrRegisterChannel("Stream", AiringChannelType.Streaming);
        harness.Schedule(
            harness.Primary,
            "tbs",
            tbs.ChannelID,
            [new AiringTrackData(AiringKind.Original, "ja")],
            (0, harness.Air(3, 22)),
            (1, harness.Air(10, 22))
        );
        harness.Schedule(
            harness.Primary,
            "stream",
            stream.ChannelID,
            [new AiringTrackData(AiringKind.Subtitled, "en")],
            (0, harness.Air(4, 1)),
            (1, harness.Air(11, 1))
        );

        var airings = harness.Service.GetAiringsInRange(
            harness.Air(3, 0),
            harness.Air(20, 0),
            new EpisodeAiringFilteringOptions()
            {
                IncludeEstimates = false,
                NextOnly = true,
                NextPer = new HashSet<AiringNextGrouping> { AiringNextGrouping.Series, AiringNextGrouping.Kind },
            }
        );

        Assert.Equal(
            [(harness.Episodes[0].ID, AiringKind.Original), (harness.Episodes[0].ID, AiringKind.Subtitled)],
            airings.Select(airing => (airing.EpisodeID, Assert.Single(airing.Tracks).Kind)).ToList()
        );
    }

    [Fact]
    public void NextOnly_AnEntityReadKeepsTheNextAiringFromNow()
    {
        using var harness = new Harness();
        var tbs = harness.Service.FindOrRegisterChannel("TBS", AiringChannelType.Television);
        harness.Schedule(
            harness.Primary,
            "tbs",
            tbs.ChannelID,
            [new AiringTrackData(AiringKind.Original, "ja")],
            (0, harness.Air(1, 22)),
            (1, harness.Air(40, 22)),
            (2, harness.Air(47, 22))
        );

        var airing = Assert.Single(harness.Service.GetAiringsForSeries(
            harness.ProviderSeries,
            new EpisodeAiringFilteringOptions() { IncludeEstimates = false, NextOnly = true }
        ));

        Assert.Equal(harness.Air(40, 22), airing.AiredAt);
    }

    [Fact]
    public void NextOnly_AnEntityReadAtAnotherTimeKeepsTheAiringNextThen()
    {
        using var harness = new Harness();
        var tbs = harness.Service.FindOrRegisterChannel("TBS", AiringChannelType.Television);
        harness.Schedule(
            harness.Primary,
            "tbs",
            tbs.ChannelID,
            [new AiringTrackData(AiringKind.Original, "ja")],
            (0, harness.Air(1, 22)),
            (1, harness.Air(40, 22)),
            (2, harness.Air(47, 22))
        );
        DateTime? NextAt(DateTime at)
            => Assert.Single(harness.Service.GetAiringsForSeries(
                harness.ProviderSeries,
                new EpisodeAiringFilteringOptions() { IncludeEstimates = false, NextOnly = true, At = at }
            )).AiredAt;

        Assert.Equal(harness.Air(1, 22), NextAt(harness.Air(1, 0)));
        Assert.Equal(harness.Air(40, 22), NextAt(harness.Air(40, 22, 10)));
        Assert.Equal(harness.Air(47, 22), NextAt(harness.Air(41, 0)));
    }

    [Fact]
    public void NextOnly_SkipsAnEpisodeAlreadyPremieredElsewhere()
    {
        using var harness = new Harness();
        var tokyo = harness.Service.FindOrRegisterChannel("TOKYO MX", AiringChannelType.Television);
        var atx = harness.Service.FindOrRegisterChannel("AT-X", AiringChannelType.Television);
        harness.Schedule(
            harness.Primary,
            "mx",
            tokyo.ChannelID,
            [new AiringTrackData(AiringKind.Original, "ja")],
            (0, harness.Air(23, 23)),
            (1, harness.Air(30, 23)),
            (2, harness.Air(37, 23))
        );
        // A week behind, so its first two episodes air after they premiered on TOKYO MX.
        harness.Schedule(
            harness.Secondary,
            "atx",
            atx.ChannelID,
            [new AiringTrackData(AiringKind.Original, "ja")],
            (0, harness.Air(32, 1)),
            (1, harness.Air(39, 1))
        );

        var airing = Assert.Single(harness.Service.GetAiringsForSeries(
            harness.ShokoSeries,
            new EpisodeAiringFilteringOptions() { IncludeEstimates = false, NextOnly = true, At = harness.Air(31, 12) }
        ));

        Assert.Equal((harness.ShokoEpisodes[2].ID, harness.Air(37, 23)), (airing.ShokoEpisode?.ID, airing.AiredAt));
    }

    [Theory]
    [InlineData(AiringChannelType.Television, 30)]
    [InlineData(AiringChannelType.Streaming, 24)]
    public void NextOnly_KeepsAnEpisodeNextWhileItIsOnAir(AiringChannelType channelType, int slotMinutes)
    {
        using var harness = new Harness();
        var channel = harness.Service.FindOrRegisterChannel("Channel", channelType);
        var start = harness.Air(40, 15, 30);
        harness.Schedule(
            harness.Primary,
            "run",
            channel.ChannelID,
            [new AiringTrackData(AiringKind.Original, "ja")],
            (0, start),
            (1, start.AddDays(7))
        );
        var options = new EpisodeAiringFilteringOptions() { IncludeEstimates = false, NextOnly = true };
        IEpisodeAiring NextAt(DateTime time)
            => Assert.Single(harness.Service.GetAiringsInRange(time, start.AddDays(14), options));

        // The episode's length is unknown, so it runs 24 minutes.
        var onAir = NextAt(start.AddMinutes(1));
        Assert.Equal((harness.Episodes[0].ID, start.AddMinutes(slotMinutes)), (onAir.EpisodeID, onAir.EndsAt));
        Assert.True(onAir.IsAiringAt(start.AddMinutes(1)));
        Assert.Equal(harness.Episodes[0].ID, NextAt(start.AddMinutes(slotMinutes - 1)).EpisodeID);
        Assert.Equal(harness.Episodes[1].ID, NextAt(start.AddMinutes(slotMinutes)).EpisodeID);
    }

    [Fact]
    public void ATelevisionSlotEndsAtTheChannelsNextAiring()
    {
        using var harness = new Harness();
        var tbs = harness.Service.FindOrRegisterChannel("TBS", AiringChannelType.Television);
        var start = harness.Air(40, 23);
        var schedule = harness.Schedule(harness.Primary, "tbs", tbs.ChannelID, [new AiringTrackData(AiringKind.Original, "ja")], (0, start));
        harness.OrphanSchedule(harness.Secondary, "other", tbs.ChannelID, start.AddMinutes(25));

        Assert.Equal(start.AddMinutes(25), Assert.Single(harness.Read(schedule)).EndsAt);
    }

    [Fact]
    public void AnEpisodesLengthFallsBackToItsAnimesUsualLength()
    {
        using var harness = new Harness();
        AniDB_Episode Episode(int id, int animeID, int lengthSeconds)
            => new() { AniDB_EpisodeID = id, EpisodeID = id, AnimeID = animeID, EpisodeType = EpisodeType.Episode, LengthSeconds = lengthSeconds };
        var episodes = new[] { Episode(1, 10, 720), Episode(2, 10, 1500), Episode(3, 10, 1500), Episode(4, 10, 0), Episode(5, 20, 0) };
        harness.UseAnidbEpisodes(episodes);
        var context = new AiringReadContext(harness.Service);

        Assert.Equal(TimeSpan.FromSeconds(720), context.GetEpisodeDuration(episodes[0]));
        Assert.Equal(TimeSpan.FromSeconds(1500), context.GetEpisodeDuration(episodes[3]));
        Assert.Null(context.GetEpisodeDuration(episodes[4]));
    }

    [Fact]
    public void GetAiringsForSeries_ManySeries_AnswersEachAsAlone()
    {
        using var harness = new Harness();
        var tokyo = harness.Service.FindOrRegisterChannel("TOKYO MX", AiringChannelType.Television);
        harness.Weekly("mx", tokyo.ChannelID, 30);
        harness.OrphanSchedule(harness.Primary, "orphan", tokyo.ChannelID, harness.Air(33, 23));
        var options = new EpisodeAiringFilteringOptions() { IncludeEstimates = false, NextOnly = true, At = harness.Air(31, 12) };

        var many = harness.Service.GetAiringsForSeries([harness.ShokoSeries, harness.OrphanSeries, harness.ShokoSeries], options);

        Assert.Equal(2, many.Count);
        foreach (var series in new[] { harness.ShokoSeries, harness.OrphanSeries })
            Assert.Equal(harness.Service.GetAiringsForSeries(series, options).Select(airing => airing.ID), many[series.ID].Select(airing => airing.ID));
    }

    [Fact]
    public void IncludeDateOnly_AddsAnEntryForAnAnidbEpisodeNoProviderKnows()
    {
        using var harness = new Harness();
        var anidbEpisode = harness.AnidbEpisode(new DateOnly(2026, 10, 4));

        Assert.Empty(harness.Service.GetAiringsForEpisode(anidbEpisode, new EpisodeAiringFilteringOptions()));
        var entry = Assert.Single(harness.Service.GetAiringsForEpisode(anidbEpisode, new EpisodeAiringFilteringOptions() { IncludeDateOnly = true }));

        Assert.True(entry.IsDateOnly);
        Assert.Equal(new DateOnly(2026, 10, 4), entry.AirDate);
        Assert.Equal((null, null, null), (entry.AiredAt, entry.Schedule, entry.ProviderID));
    }

    [Fact]
    public void IncludeDateOnly_LeavesOutAnEpisodeWithAnAiringOrAProviderFilter()
    {
        using var harness = new Harness();
        var tbs = harness.Service.FindOrRegisterChannel("TBS", AiringChannelType.Television);
        harness.Schedule(harness.Primary, "tbs", tbs.ChannelID, [new AiringTrackData(AiringKind.Original, "ja")], (0, harness.Air(3, 22)));
        var known = harness.AnidbEpisode(new DateOnly(2026, 10, 4), harness.ShokoEpisodes[0]);
        var unknown = harness.AnidbEpisode(new DateOnly(2026, 10, 4));
        var providerID = harness.Service.GetProviderInfo(harness.Primary).ID;

        // An airing on a linked episode is still an airing, even when the read does not walk the links.
        Assert.Empty(harness.Service.GetAiringsForEpisode(known, new EpisodeAiringFilteringOptions()
        {
            IncludeDateOnly = true,
            LinkedEntityAirings = false,
        }));
        Assert.Empty(harness.Service.GetAiringsForEpisode(unknown, new EpisodeAiringFilteringOptions()
        {
            IncludeDateOnly = true,
            ProviderIDs = new HashSet<Guid> { providerID },
        }));
    }

    [Theory]
    [InlineData("2026-10-04T00:00:00+09:00", "2026-10-05T00:00:00+09:00", true)]
    [InlineData("2026-10-03T00:00:00Z", "2026-10-04T00:00:00Z", false)]
    [InlineData("2026-10-04T00:00:00Z", "2026-10-04T00:00:01Z", true)]
    [InlineData("2026-10-05T00:00:00+09:00", "2026-10-06T00:00:00+09:00", false)]
    public void IncludeDateOnly_RangeReadMatchesTheDateOnTheCallersCalendar(string from, string to, bool included)
    {
        using var harness = new Harness();
        harness.UseAnidbEpisodes(new AniDB_Episode
        {
            AniDB_EpisodeID = 1,
            EpisodeID = 5000,
            AnimeID = 500,
            EpisodeNumber = 1,
            EpisodeType = EpisodeType.Episode,
            AirDate = (int)new DateTimeOffset(2026, 10, 4, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds(),
        });

        var airings = harness.Service.GetAiringsInRange(
            DateTimeOffset.Parse(from, CultureInfo.InvariantCulture),
            DateTimeOffset.Parse(to, CultureInfo.InvariantCulture),
            new EpisodeAiringFilteringOptions() { IncludeDateOnly = true }
        );

        Assert.Equal(included, airings.Any(airing => airing.IsDateOnly && airing.EpisodeID.ID == "5000"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(null)]
    public void IncludeDateOnly_DatesAPre1970EpisodeByTheEarliestLinkedEpisode(int? airDate)
    {
        using var harness = new Harness();
        var episode = harness.UseUndatedAnidbEpisode(new PartialDateOnly(1965, 4), airDate);
        harness.UseEpisodeLinks(
            harness.EpisodeLink(TestSources.Plugin, "7101", () => new DateOnly(1965, 4, 10)),
            harness.EpisodeLink(TestSources.AniList, "7201", () => new DateOnly(1965, 4, 3))
        );

        var entry = Assert.Single(harness.Service.GetAiringsForEpisode(episode, new EpisodeAiringFilteringOptions() { IncludeDateOnly = true }));

        Assert.True(entry.IsDateOnly);
        Assert.Equal(new DateOnly(1965, 4, 3), entry.AirDate);
    }

    [Fact]
    public void IncludeDateOnly_NeverReadsLinksForALaterAnimeOrALaterRange()
    {
        using var harness = new Harness();
        var episode = harness.UseUndatedAnidbEpisode(new PartialDateOnly(1975, 4));
        harness.UseEpisodeLinks(harness.EpisodeLink(TestSources.Plugin, "7101", () => new DateOnly(1975, 4, 10)));
        var options = new EpisodeAiringFilteringOptions() { IncludeDateOnly = true };

        Assert.Empty(harness.Service.GetAiringsForEpisode(episode, options));
        Assert.Empty(harness.Service.GetAiringsInRange(
            new DateTimeOffset(1975, 4, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(1975, 5, 1, 0, 0, 0, TimeSpan.Zero),
            options
        ));
        harness.CrossReferences.Verify(store => store.GetEpisodeLinksForSeries(It.IsAny<int>(), It.IsAny<MetadataSource?>()), Times.Never());
    }

    [Theory]
    [InlineData("1965-04-03T00:00:00Z", "1965-04-04T00:00:00Z", true)]
    [InlineData("1965-04-04T00:00:00Z", "1965-05-01T00:00:00Z", false)]
    public void IncludeDateOnly_RangeReadFindsALinkedPre1970Date(string from, string to, bool included)
    {
        using var harness = new Harness();
        harness.UseUndatedAnidbEpisode(new PartialDateOnly(1965, 4));
        harness.UseEpisodeLinks(harness.EpisodeLink(TestSources.Plugin, "7101", () => new DateOnly(1965, 4, 3)));

        var airings = harness.Service.GetAiringsInRange(
            DateTimeOffset.Parse(from, CultureInfo.InvariantCulture),
            DateTimeOffset.Parse(to, CultureInfo.InvariantCulture),
            new EpisodeAiringFilteringOptions() { IncludeDateOnly = true }
        );

        Assert.Equal(included, airings.Any(airing => airing.IsDateOnly && airing.EpisodeID.ID == Harness.UndatedEpisodeID.ToString()));
    }

    [Fact]
    public void IncludeDateOnly_APlaceholderTheAnimesDateStandsInForIsNotLinked()
    {
        using var harness = new Harness();
        harness.UseUndatedAnidbEpisode(new PartialDateOnly(1965, 4, 3));
        harness.UseEpisodeLinks(harness.EpisodeLink(TestSources.Plugin, "7101", () => new DateOnly(1965, 4, 10)));

        var airings = harness.Service.GetAiringsInRange(
            new DateTimeOffset(1965, 4, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(1965, 5, 1, 0, 0, 0, TimeSpan.Zero),
            new EpisodeAiringFilteringOptions() { IncludeDateOnly = true }
        );

        var entry = Assert.Single(airings, airing => airing.IsDateOnly && airing.EpisodeID.ID == Harness.UndatedEpisodeID.ToString());
        Assert.Equal(new DateOnly(1965, 4, 3), entry.AirDate);
        harness.CrossReferences.Verify(store => store.GetEpisodeLinksForSeries(It.IsAny<int>(), It.IsAny<MetadataSource?>()), Times.Never());
    }

    [Fact]
    public void IncludeDateOnly_LinkedDatesAreKeptUntilALinkOrALinkedEpisodeChanges()
    {
        using var harness = new Harness();
        var episode = harness.UseUndatedAnidbEpisode(new PartialDateOnly(1965, 4));
        var airDate = new DateOnly(1965, 4, 3);
        var link = harness.EpisodeLink(TestSources.Plugin, "7101", () => airDate);
        harness.UseEpisodeLinks(link);
        var options = new EpisodeAiringFilteringOptions() { IncludeDateOnly = true };
        DateOnly? Read() => harness.Service.GetAiringsForEpisode(episode, options).SingleOrDefault()?.AirDate;

        Assert.Equal(new DateOnly(1965, 4, 3), Read());
        airDate = new DateOnly(1965, 4, 17);
        Assert.Equal(new DateOnly(1965, 4, 3), Read());

        harness.Metadata.Raise(
            service => service.EpisodeUpdated += null,
            new EpisodeInfoUpdatedEventArgs(Mock.Of<ISeries>(), (IEpisode)link.Provider!, UpdateReason.Updated)
        );
        Assert.Equal(new DateOnly(1965, 4, 17), Read());

        harness.UseEpisodeLinks();
        harness.Linking.Raise(
            service => service.LinksChanged += null,
            new MetadataLinksChangedEventArgs()
            {
                Reason = MetadataLinkChangeReason.Manual,
                Changes =
                [
                    new MetadataLinkChange()
                    {
                        Kind = MetadataLinkChangeKind.Removed,
                        Source = TestSources.Plugin,
                        EntityType = MetadataEntityType.Episode,
                        AnidbAnimeID = Harness.UndatedAnimeID,
                    },
                ],
            }
        );
        Assert.Null(Read());
    }

    [Theory]
    [InlineData(InclusionFilter.Only, true, false)]
    [InlineData(InclusionFilter.False, false, true)]
    [InlineData(InclusionFilter.True, true, true)]
    public void SeriesFilters_SplitOnTheCollection(InclusionFilter inCollection, bool inIt, bool notInIt)
    {
        var options = new EpisodeAiringFilteringOptions() { InCollection = inCollection };

        Assert.Equal(
            (inIt, notInIt),
            (
                AiringScheduleService.PassesSeriesFilters(new AiringSeriesState { IsInCollection = true }, options),
                AiringScheduleService.PassesSeriesFilters(AiringSeriesState.Unknown, options)
            )
        );
    }

    [Fact]
    public void SeriesFilters_AFilterKeepsOnlyTheSeriesItPassed()
    {
        var options = new EpisodeAiringFilteringOptions() { Filter = Mock.Of<IFilter>() };
        var passed = new HashSet<int> { 1 };

        Assert.Equal(
            (true, false, false),
            (
                AiringScheduleService.PassesSeriesFilters(new AiringSeriesState { IsInCollection = true, AnidbAnimeID = 1 }, options, passed),
                AiringScheduleService.PassesSeriesFilters(new AiringSeriesState { IsInCollection = true, AnidbAnimeID = 2 }, options, passed),
                AiringScheduleService.PassesSeriesFilters(AiringSeriesState.Unknown, options, passed)
            )
        );
    }

    [Fact]
    public void AReadEvaluatesItsFilterOnce()
    {
        using var harness = new Harness();
        var filter = Mock.Of<IFilter>();
        var series = Mock.Of<IShokoSeries>(entry => entry.AnidbAnimeID == 1);
        harness.Filtering
            .Setup(service => service.GetAllFilteredSeries(filter, null, null, true, It.IsAny<CancellationToken>()))
            .Returns([series]);
        var context = new AiringReadContext(harness.Service);

        Assert.Equal([1], context.GetFilteredAnimeIDs(filter, null));
        Assert.Equal([1], context.GetFilteredAnimeIDs(filter, null));
        harness.Filtering.Verify(
            service => service.GetAllFilteredSeries(It.IsAny<IFilter>(), It.IsAny<IUser?>(), It.IsAny<DateTime?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    #endregion

    #region Reruns

    [Fact]
    public void AMarathonAfterAnEarlierRunIsADetectedRerun()
    {
        using var harness = new Harness();
        var tokyo = harness.Service.FindOrRegisterChannel("TOKYO MX", AiringChannelType.Television);
        var bs11 = harness.Service.FindOrRegisterChannel("BS11", AiringChannelType.Television);
        var original = harness.Weekly("mx", tokyo.ChannelID, 1);
        var marathon = harness.Schedule(
            harness.Primary,
            "bs11",
            bs11.ChannelID,
            [new AiringTrackData(AiringKind.Original, "ja")],
            (0, harness.Air(29, 13)),
            (1, harness.Air(29, 14)),
            (2, harness.Air(29, 15)),
            (3, harness.Air(29, 16))
        );

        Assert.All(harness.Read(marathon), airing => Assert.Equal(EpisodeAiringKind.DetectedRerun, airing.Kind));
        Assert.All(harness.Read(original), airing => Assert.Equal(EpisodeAiringKind.Normal, airing.Kind));
    }

    [Fact]
    public void AStreamingDropWithNoEarlierAiringStaysNormal()
    {
        using var harness = new Harness();
        var netflix = harness.Service.FindOrRegisterChannel("Netflix", AiringChannelType.Streaming);
        var tokyo = harness.Service.FindOrRegisterChannel("TOKYO MX", AiringChannelType.Television);
        var drop = harness.Schedule(
            harness.Primary,
            "netflix",
            netflix.ChannelID,
            [new AiringTrackData(AiringKind.Original, "ja")],
            (0, harness.Air(1, 8)),
            (1, harness.Air(1, 8)),
            (2, harness.Air(1, 8)),
            (3, harness.Air(1, 8))
        );
        harness.Weekly("mx", tokyo.ChannelID, 8);

        Assert.All(harness.Read(drop), airing => Assert.Equal(EpisodeAiringKind.Normal, airing.Kind));
    }

    [Theory]
    [InlineData(15, EpisodeAiringKind.Normal)]
    [InlineData(71, EpisodeAiringKind.DetectedRerun)]
    public void ALateRunIsADetectedRerunAndARegionalOneIsNot(int firstDay, EpisodeAiringKind expected)
    {
        using var harness = new Harness();
        var tokyo = harness.Service.FindOrRegisterChannel("TOKYO MX", AiringChannelType.Television);
        var regional = harness.Service.FindOrRegisterChannel("テレビ愛知", AiringChannelType.Television);
        harness.Weekly("mx", tokyo.ChannelID, 1);
        var later = harness.Weekly("aichi", regional.ChannelID, firstDay);

        Assert.All(harness.Read(later), airing => Assert.Equal(expected, airing.Kind));
    }

    [Fact]
    public void ADetectedRerunKeepsTheKindsTheProviderGave()
    {
        using var harness = new Harness();
        var tokyo = harness.Service.FindOrRegisterChannel("TOKYO MX", AiringChannelType.Television);
        var atx = harness.Service.FindOrRegisterChannel("AT-X", AiringChannelType.Television);
        harness.Weekly("mx", tokyo.ChannelID, 1);
        var schedule = harness.Service.AddOrUpdateSchedule(harness.Primary, harness.ScheduleData("atx", atx.ChannelID));
        harness.Service.SetAirings(harness.Primary, schedule, [
            new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = harness.Air(71, 23) },
            new EpisodeAiringData() { Episode = harness.Episodes[1], AiredAt = harness.Air(64, 23), Key = "advance", Kind = EpisodeAiringKind.Advance },
            new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = harness.Air(74, 2), Key = "rerun", Kind = EpisodeAiringKind.Rerun },
        ]);

        var airings = harness.Read(schedule);

        Assert.Equal(EpisodeAiringKind.DetectedRerun, Assert.Single(airings, airing => airing.Key is not ("advance" or "rerun")).Kind);
        Assert.Equal(EpisodeAiringKind.Advance, Assert.Single(airings, airing => airing.Key == "advance").Kind);
        Assert.Equal(EpisodeAiringKind.Rerun, Assert.Single(airings, airing => airing.Key == "rerun").Kind);
    }

    [Fact]
    public void GetAiringsInRange_EpisodeKindsLeavesOutTheDetectedReruns()
    {
        using var harness = new Harness();
        var (late, regional) = harness.LateAndRegionalRuns();

        var all = harness.Service.GetAiringsInRange(
            harness.Air(60, 0),
            harness.Air(100, 0),
            new EpisodeAiringFilteringOptions() { IncludeEstimates = false }
        );
        var normal = harness.Service.GetAiringsInRange(
            harness.Air(60, 0),
            harness.Air(100, 0),
            new EpisodeAiringFilteringOptions() { IncludeEstimates = false, EpisodeKinds = new HashSet<EpisodeAiringKind> { EpisodeAiringKind.Normal } }
        );

        Assert.Contains(all, airing => airing.Schedule?.ID == late.ID);
        Assert.Equal(regional.ID, Assert.Single(normal).Schedule?.ID);
    }

    [Fact]
    public void NextOnly_ALateRunOfEpisodesAlreadyShownIsNeverNext()
    {
        using var harness = new Harness();
        harness.LateAndRegionalRuns();

        var next = harness.Service.GetAiringsForSeries(
            harness.ProviderSeries,
            new EpisodeAiringFilteringOptions() { IncludeEstimates = false, NextOnly = true, At = harness.Air(31, 12) }
        );

        Assert.Empty(next);
    }

    #endregion

    #region Anime by Channel

    [Fact]
    public void GetAnidbAnimeOnChannels_GathersTheQuartersAndWhetherOneIsToCome()
    {
        using var harness = new Harness();
        var anidbEpisode = new Mock<IAnidbEpisode>();
        anidbEpisode.SetupGet(entry => entry.AnidbAnimeID).Returns(900);
        foreach (var shokoEpisode in harness.ShokoEpisodes)
            Mock.Get(shokoEpisode).SetupGet(entry => entry.AnidbEpisode).Returns(anidbEpisode.Object);
        var tokyo = harness.Service.FindOrRegisterChannel("TOKYO MX", AiringChannelType.Television);
        var atx = harness.Service.FindOrRegisterChannel("AT-X", AiringChannelType.Television);
        var bs11 = harness.Service.FindOrRegisterChannel("BS11", AiringChannelType.Television);
        harness.Schedule(harness.Primary, "mx", tokyo.ChannelID, [new AiringTrackData(AiringKind.Original, "ja")], (0, harness.Air(1, 23)));
        harness.Schedule(harness.Primary, "atx", atx.ChannelID, [new AiringTrackData(AiringKind.Original, "ja")], (1, harness.Air(40, 23)));

        var onTokyo = harness.Service.GetAnidbAnimeOnChannels(new HashSet<Guid> { tokyo.ChannelID });
        var onAtx = harness.Service.GetAnidbAnimeOnChannels(new HashSet<Guid> { atx.ChannelID });

        Assert.Equal([SeasonCalendar.GetCalendarQuarter(DateOnly.FromDateTime(harness.Air(1, 23)))], onTokyo[900].Seasons);
        Assert.False(onTokyo[900].HasUpcoming);
        Assert.True(onAtx[900].HasUpcoming);
        Assert.Empty(harness.Service.GetAnidbAnimeOnChannels(new HashSet<Guid> { bs11.ChannelID }));
    }

    #endregion

    #region Hidden Channels

    [Fact]
    public void AHiddenChannelIsLeftOutUnlessNamed()
    {
        using var harness = new Harness();
        var tokyo = harness.Service.FindOrRegisterChannel("TOKYO MX", AiringChannelType.Television);
        var bs11 = harness.Service.FindOrRegisterChannel("BS11", AiringChannelType.Television);
        harness.Schedule(harness.Primary, "mx", tokyo.ChannelID, [new AiringTrackData(AiringKind.Original, "ja")], (0, harness.Air(1, 23)));
        var hidden = harness.Schedule(harness.Primary, "bs11", bs11.ChannelID, [new AiringTrackData(AiringKind.Original, "ja")], (0, harness.Air(1, 23, 30)));
        harness.Service.SetChannelHidden(bs11, true);

        var visible = harness.Service.GetAiringsForEpisode(harness.Episodes[0], new EpisodeAiringFilteringOptions() { IncludeEstimates = false });
        var named = harness.Service.GetAiringsForEpisode(
            harness.Episodes[0],
            new EpisodeAiringFilteringOptions() { IncludeEstimates = false, ChannelIDs = new HashSet<Guid> { bs11.ChannelID } }
        );

        Assert.Equal(tokyo.ChannelID, Assert.Single(visible).Channel?.ChannelID);
        Assert.Equal(bs11.ChannelID, Assert.Single(named).Channel?.ChannelID);
        Assert.Single(harness.Read(hidden));
    }

    [Fact]
    public void TheHiddenStateIsTheChannels()
    {
        using var harness = new Harness();
        var tokyo = harness.Service.FindOrRegisterChannel("TOKYO MX", AiringChannelType.Television);
        var bs11 = harness.Service.FindOrRegisterChannel("BS11", AiringChannelType.Television);
        harness.Service.SetChannelHidden(bs11, true);

        Assert.Equal([bs11.ChannelID], harness.Service.HiddenChannelIDs);
        Assert.True(harness.Service.GetChannelByID(bs11.ChannelID)?.IsHidden);
        Assert.False(harness.Service.GetChannelByID(tokyo.ChannelID)?.IsHidden);
        Assert.Equal([bs11.ChannelID], harness.Service.GetAllChannels().Where(channel => channel.IsHidden).Select(channel => channel.ChannelID));
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

        private readonly List<ISeason> _seasons = [];

        private static readonly DateTime _firstAirDate = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(-30), DateTimeKind.Utc);

        public AiringScheduleServiceSettings Settings { get; } = new();

        public Mock<AiringScheduleRepository> Schedules { get; }

        public Mock<EpisodeAiringRepository> Airings { get; }

        public Mock<AiringChannelRepository> Channels { get; }

        public Mock<ShokoImage_EntityRepository> Images { get; }

        /// <summary>The service's settings provider, over <see cref="Settings"/>.</summary>
        public ConfigurationProvider<AiringScheduleServiceSettings> ConfigurationProvider { get; }

        public Mock<ScheduledUpdateRepository> Updates { get; }

        public Mock<IMetadataService> Metadata { get; } = new();

        public Mock<IMetadataCrossReferenceStore> CrossReferences { get; } = new();

        public Mock<IMetadataLinkingService> Linking { get; } = new();

        public Mock<IMetadataFilteringService> Filtering { get; } = new();

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
            Channels.Setup(repository => repository.Delete(It.IsAny<AiringChannel>())).Callback<AiringChannel>(entry => Channels.Object.Cache.Remove(entry));
            Images = CachedRepo.BuildWritable<ShokoImage_EntityRepository, int, ShokoImage_Entity>(entry => entry.ID);
            var nextImageID = 1;
            Images.Setup(repository => repository.Save(It.IsAny<ShokoImage_Entity>())).Callback<ShokoImage_Entity>(entry =>
            {
                if (entry.ID is 0)
                    entry.ID = nextImageID++;
                Images.Object.Cache.Update(entry);
            });
            Images.Setup(repository => repository.Delete(It.IsAny<IReadOnlyCollection<ShokoImage_Entity>>()))
                .Callback<IReadOnlyCollection<ShokoImage_Entity>>(entries =>
                {
                    foreach (var entry in entries.ToList())
                        Images.Object.Cache.Remove(entry);
                });
            // The ticker's watermark is an ordinary ScheduledUpdate row, so the
            // direct repository is mocked down to the two calls it makes.
            Updates = new Mock<ScheduledUpdateRepository>((DatabaseFactory)null!);
            Updates.Setup(repository => repository.GetByUpdateType(It.IsAny<int>())).Returns(() => Watermark);
            Updates.Setup(repository => repository.Save(It.IsAny<ScheduledUpdate>())).Callback<ScheduledUpdate>(row => Watermark = row);
            _scope.Set(Schedules.Object).Set(Airings.Object).Set(Channels.Object).Set(Images.Object).Set(Updates.Object);

            var shokoSeries = new Mock<IShokoSeries>();
            var linkedSeries = new Mock<ISeries>();
            linkedSeries.SetupGet(series => series.ID).Returns(new MetadataGuid(TestSources.Plugin, MetadataEntityType.Series, "2"));
            linkedSeries.SetupGet(series => series.Source).Returns(TestSources.Plugin);
            linkedSeries.SetupGet(series => series.EntityType).Returns(MetadataEntityType.Series);
            shokoSeries.SetupGet(series => series.LocalID).Returns(1);
            shokoSeries.SetupGet(series => series.ID).Returns(new MetadataGuid(TestSources.Plugin, MetadataEntityType.Series, "1"));
            shokoSeries.SetupGet(series => series.Source).Returns(TestSources.Plugin);
            shokoSeries.SetupGet(series => series.EntityType).Returns(MetadataEntityType.Series);
            shokoSeries.SetupGet(series => series.LinkedSeries).Returns([linkedSeries.Object]);
            ProviderSeries = linkedSeries.Object;
            ShokoSeries = shokoSeries.Object;

            var episodes = new List<IEpisode>();
            var shokoEpisodes = new List<IShokoEpisode>();
            for (var index = 0; index < 4; index++)
            {
                var episode = new Mock<IEpisode>();
                var shokoEpisode = new Mock<IShokoEpisode>();
                episode.SetupGet(entry => entry.ID).Returns(new MetadataGuid(TestSources.Plugin, MetadataEntityType.Episode, (100 + index).ToString()));
                episode.SetupGet(entry => entry.Source).Returns(TestSources.Plugin);
                episode.SetupGet(entry => entry.EntityType).Returns(MetadataEntityType.Episode);
                episode.SetupGet(entry => entry.SeriesID).Returns(new MetadataGuid(TestSources.Plugin, MetadataEntityType.Series, "2"));
                episode.SetupGet(entry => entry.Type).Returns(EpisodeType.Episode);
                episode.SetupGet(entry => entry.EpisodeNumber).Returns(index + 1);
                episode.SetupGet(entry => entry.AirDate).Returns(DateOnly.FromDateTime(_firstAirDate.AddDays(7 * index)));
                episode.SetupGet(entry => entry.ShokoEpisodes).Returns(() => [shokoEpisode.Object]);
                shokoEpisode.SetupGet(entry => entry.LocalID).Returns(200 + index);
                shokoEpisode.SetupGet(entry => entry.ID).Returns(new MetadataGuid(TestSources.Plugin, MetadataEntityType.Episode, (200 + index).ToString()));
                shokoEpisode.SetupGet(entry => entry.Source).Returns(TestSources.Plugin);
                shokoEpisode.SetupGet(entry => entry.EntityType).Returns(MetadataEntityType.Episode);
                shokoEpisode.SetupGet(entry => entry.ShokoSeriesID).Returns(1);
                shokoEpisode.SetupGet(entry => entry.SeriesID).Returns(new MetadataGuid(TestSources.Plugin, MetadataEntityType.Series, "1"));
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
            orphanSeries.SetupGet(series => series.ID).Returns(new MetadataGuid(TestSources.Plugin, MetadataEntityType.Series, "3"));
            orphanSeries.SetupGet(series => series.Source).Returns(TestSources.Plugin);
            orphanSeries.SetupGet(series => series.EntityType).Returns(MetadataEntityType.Series);
            orphanSeries.SetupGet(series => series.ShokoSeries).Returns([]);
            var orphanEpisode = new Mock<IEpisode>();
            orphanEpisode.SetupGet(entry => entry.ID).Returns(new MetadataGuid(TestSources.Plugin, MetadataEntityType.Episode, "300"));
            orphanEpisode.SetupGet(entry => entry.Source).Returns(TestSources.Plugin);
            orphanEpisode.SetupGet(entry => entry.EntityType).Returns(MetadataEntityType.Episode);
            orphanEpisode.SetupGet(entry => entry.SeriesID).Returns(new MetadataGuid(TestSources.Plugin, MetadataEntityType.Series, "3"));
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
            shokoSeries.As<ISeries>().SetupGet(series => series.Episodes).Returns(() => shokoEpisodes);
            linkedSeries.SetupGet(series => series.Episodes).Returns(() => episodes);

            var configurationInfo = (ConfigurationInfo)RuntimeHelpers.GetUninitializedObject(typeof(ConfigurationInfo));
            var configurationService = new Mock<IConfigurationService>();
            configurationService.Setup(service => service.GetConfigurationInfo<AiringScheduleServiceSettings>()).Returns(configurationInfo);
            configurationService.Setup(service => service.Load(It.IsAny<ConfigurationInfo>(), It.IsAny<bool>())).Returns(Settings);
            var pluginManager = new Mock<IPluginManager>();
            pluginManager.Setup(manager => manager.GetPluginInfo(It.IsAny<Assembly>()))
                .Returns(PluginTestDoubles.CorePluginInfo(typeof(CorePlugin), CorePlugin.StaticID));

            Metadata.Setup(service => service.GetEntry(It.IsAny<MetadataGuid>())).Returns((MetadataGuid id) => Resolve(id));
            CrossReferences.Setup(store => store.GetSeriesLinks(It.IsAny<int>(), It.IsAny<MetadataSource?>())).Returns([]);
            CrossReferences.Setup(store => store.GetEpisodeLinksForSeries(It.IsAny<int>(), It.IsAny<MetadataSource?>())).Returns([]);
            CrossReferences.Setup(store => store.GetLinksTo(It.IsAny<MetadataGuid>())).Returns([]);
            ConfigurationProvider = new ConfigurationProvider<AiringScheduleServiceSettings>(configurationService.Object);
            Service = new AiringScheduleService(
                NullLogger<AiringScheduleService>.Instance,
                configurationService.Object,
                pluginManager.Object,
                Mock.Of<IApplicationPaths>(),
                new Mock<IQueueScheduler>().Object,
                ConfigurationProvider,
                new(() => Metadata.Object),
                new(() => CrossReferences.Object),
                new(() => Linking.Object),
                new(() => Filtering.Object)
            );
            Service.AddParts([Primary, Secondary]);
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
        ///   A weekly run of all four episodes on one channel, the first on <paramref name="firstDay"/>.
        /// </summary>
        public IAiringSchedule Weekly(string key, Guid channelID, int firstDay)
            => Schedule(
                Primary,
                key,
                channelID,
                [new AiringTrackData(AiringKind.Original, "ja")],
                (0, Air(firstDay, 23)),
                (1, Air(firstDay + 7, 23)),
                (2, Air(firstDay + 14, 23)),
                (3, Air(firstDay + 21, 23))
            );

        /// <summary>
        ///   An original weekly run, a whole rerun of it ten weeks later, and a regional channel
        ///   that only picks up the last episode some weeks behind the original, after the rerun
        ///   started.
        /// </summary>
        public (IAiringSchedule Late, IAiringSchedule Regional) LateAndRegionalRuns()
        {
            var tokyo = Service.FindOrRegisterChannel("TOKYO MX", AiringChannelType.Television);
            var atx = Service.FindOrRegisterChannel("AT-X", AiringChannelType.Television);
            var aichi = Service.FindOrRegisterChannel("テレビ愛知", AiringChannelType.Television);
            Weekly("mx", tokyo.ChannelID, 1);
            var late = Weekly("atx", atx.ChannelID, 71);
            var regional = Schedule(Primary, "aichi", aichi.ChannelID, [new AiringTrackData(AiringKind.Original, "ja")], (3, Air(75, 23)));
            return (late, regional);
        }

        /// <summary>Every stored airing of a schedule, as a read hands it back.</summary>
        public IReadOnlyList<IEpisodeAiring> Read(IAiringSchedule schedule)
            => Service.GetAiringsForSchedule(schedule.ID, new EpisodeAiringFilteringOptions() { IncludeEstimates = false });

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
                tokyo.ChannelID,
                [new AiringTrackData(AiringKind.Original, "ja")],
                (0, Air(1, 0, 0) + offset),
                (1, Air(8, 0, 0) + offset)
            );
        }

        /// <summary>
        ///   An AniDB episode with an air date and nothing else, linked to the given shoko
        ///   episode when there is one.
        /// </summary>
        public IAnidbEpisode AnidbEpisode(DateOnly airDate, IShokoEpisode? shokoEpisode = null)
        {
            var episode = new Mock<IAnidbEpisode>();
            episode.SetupGet(entry => entry.ID).Returns(new MetadataGuid(MetadataSource.AniDB, MetadataEntityType.Episode, "9000"));
            episode.SetupGet(entry => entry.Source).Returns(MetadataSource.AniDB);
            episode.SetupGet(entry => entry.EntityType).Returns(MetadataEntityType.Episode);
            episode.SetupGet(entry => entry.SeriesID).Returns(new MetadataGuid(MetadataSource.AniDB, MetadataEntityType.Series, "900"));
            episode.SetupGet(entry => entry.Type).Returns(EpisodeType.Episode);
            episode.SetupGet(entry => entry.EpisodeNumber).Returns(1);
            episode.SetupGet(entry => entry.AirDate).Returns(airDate);
            episode.SetupGet(entry => entry.ShokoEpisodes).Returns(shokoEpisode is null ? [] : [shokoEpisode]);
            return episode.Object;
        }

        /// <summary>
        ///   Installs the AniDB episodes a range read looks date-only entries up in, with no
        ///   anime and no shoko episodes behind them.
        /// </summary>
        public void UseAnidbEpisodes(params AniDB_Episode[] episodes)
            => _scope
                .With<AniDB_EpisodeRepository, int, AniDB_Episode>(entry => entry.AniDB_EpisodeID, episodes)
                .With<AniDB_AnimeRepository, int, AniDB_Anime>(entry => entry.AniDB_AnimeID, [])
                .With<AnimeEpisodeRepository, int, AnimeEpisode>(entry => entry.AnimeEpisodeID, []);

        /// <summary>The AniDB anime <see cref="UseUndatedAnidbEpisode"/> installs.</summary>
        public const int UndatedAnimeID = 700;

        /// <summary>The AniDB episode <see cref="UseUndatedAnidbEpisode"/> installs.</summary>
        public const int UndatedEpisodeID = 7000;

        /// <summary>
        ///   Installs one anime starting on the given date with one regular episode carrying AniDB's
        ///   1970-01-01 placeholder, or no date at all, and returns the episode.
        /// </summary>
        public AniDB_Episode UseUndatedAnidbEpisode(PartialDateOnly animeAirDate, int? airDate = 0)
        {
            var episode = new AniDB_Episode
            {
                AniDB_EpisodeID = 1,
                EpisodeID = UndatedEpisodeID,
                AnimeID = UndatedAnimeID,
                EpisodeNumber = 1,
                EpisodeType = EpisodeType.Episode,
                AirDate = airDate,
            };
            _scope
                .With<AniDB_EpisodeRepository, int, AniDB_Episode>(entry => entry.AniDB_EpisodeID, [episode])
                .With<AniDB_AnimeRepository, int, AniDB_Anime>(
                    entry => entry.AniDB_AnimeID,
                    [new AniDB_Anime { AniDB_AnimeID = 1, AnimeID = UndatedAnimeID, AirDate = animeAirDate }]
                )
                .With<AnimeEpisodeRepository, int, AnimeEpisode>(entry => entry.AnimeEpisodeID, []);
            return episode;
        }

        /// <summary>
        ///   A link from the undated AniDB episode to another source's episode, whose air date is
        ///   read each time it is asked for.
        /// </summary>
        public IMetadataEpisodeCrossReference EpisodeLink(MetadataSource source, string id, Func<DateOnly?> airDate)
        {
            var providerID = new MetadataGuid(source, MetadataEntityType.Episode, id);
            var provider = new Mock<IEpisode>();
            provider.SetupGet(entry => entry.ID).Returns(providerID);
            provider.SetupGet(entry => entry.AirDate).Returns(airDate);
            var link = new Mock<IMetadataEpisodeCrossReference>();
            link.SetupGet(entry => entry.AnidbAnimeID).Returns(UndatedAnimeID);
            link.SetupGet(entry => entry.AnidbEpisodeID).Returns(UndatedEpisodeID);
            link.SetupGet(entry => entry.Source).Returns(source);
            link.SetupGet(entry => entry.ProviderID).Returns(providerID);
            link.SetupGet(entry => entry.Provider).Returns(provider.Object);
            return link.Object;
        }

        /// <summary>The AniDB anime <see cref="UseLinkedAnime"/> installs.</summary>
        public const int LinkedAnimeID = 800;

        /// <summary>
        ///   Installs an AniDB anime of four regular episodes, numbered 1 to 4 and aired a week
        ///   apart from <paramref name="firstDay"/> on, linked to <see cref="ProviderSeries"/>. Each
        ///   pair links an AniDB episode to a provider episode by their numbers, and every other
        ///   episode carries a link to nothing, the way a source that does not list it yet leaves it.
        /// </summary>
        public IReadOnlyList<AniDB_Episode> UseLinkedAnime(int firstDay, params (int Anidb, int Provider)[] links)
        {
            var episodes = Enumerable.Range(1, 4)
                .Select(number => new AniDB_Episode
                {
                    AniDB_EpisodeID = number,
                    EpisodeID = 8000 + number,
                    AnimeID = LinkedAnimeID,
                    EpisodeNumber = number,
                    EpisodeType = EpisodeType.Episode,
                    AirDate = (int)new DateTimeOffset(_firstAirDate.AddDays(firstDay - 1 + 7 * (number - 1))).ToUnixTimeSeconds(),
                })
                .ToList();
            _scope
                .With<AniDB_EpisodeRepository, int, AniDB_Episode>(entry => entry.AniDB_EpisodeID, episodes)
                .With<AniDB_AnimeRepository, int, AniDB_Anime>(entry => entry.AniDB_AnimeID, [])
                .With<AnimeEpisodeRepository, int, AnimeEpisode>(entry => entry.AnimeEpisodeID, []);

            var seriesLink = new Mock<IMetadataSeriesCrossReference>();
            seriesLink.SetupGet(entry => entry.AnidbAnimeID).Returns(LinkedAnimeID);
            seriesLink.SetupGet(entry => entry.Source).Returns(TestSources.Plugin);
            seriesLink.SetupGet(entry => entry.ProviderID).Returns(ProviderSeries.ID);
            CrossReferences
                .Setup(store => store.GetSeriesLinks(LinkedAnimeID, It.IsAny<MetadataSource?>()))
                .Returns([seriesLink.Object]);
            CrossReferences
                .Setup(store => store.GetLinksTo(It.Is<MetadataGuid>(id => id == ProviderSeries.ID)))
                .Returns([seriesLink.Object]);

            var episodeLinks = episodes
                .Select(episode =>
                {
                    var provider = links.Where(pair => pair.Anidb == episode.EpisodeNumber).Select(pair => Episodes[pair.Provider - 1].ID).FirstOrDefault();
                    var link = new Mock<IMetadataEpisodeCrossReference>();
                    link.SetupGet(entry => entry.AnidbAnimeID).Returns(LinkedAnimeID);
                    link.SetupGet(entry => entry.AnidbEpisodeID).Returns(episode.EpisodeID);
                    link.SetupGet(entry => entry.Source).Returns(TestSources.Plugin);
                    link.SetupGet(entry => entry.ProviderID).Returns(provider);
                    return link.Object;
                })
                .ToList();
            CrossReferences
                .Setup(store => store.GetEpisodeLinksForSeries(LinkedAnimeID, It.IsAny<MetadataSource?>()))
                .Returns(episodeLinks);
            return episodes;
        }

        /// <summary>
        ///   A season of <see cref="ProviderSeries"/> holding the provider episodes at the given
        ///   indexes, resolvable by its ID from then on.
        /// </summary>
        public ISeason ProviderSeason(string id, params int[] episodeIndexes)
        {
            var season = new Mock<ISeason>();
            season.SetupGet(entry => entry.ID).Returns(new MetadataGuid(TestSources.Plugin, MetadataEntityType.Season, id));
            season.SetupGet(entry => entry.Source).Returns(TestSources.Plugin);
            season.SetupGet(entry => entry.EntityType).Returns(MetadataEntityType.Season);
            season.SetupGet(entry => entry.SeriesID).Returns(ProviderSeries.ID);
            season.SetupGet(entry => entry.Series).Returns(ProviderSeries);
            season.SetupGet(entry => entry.Episodes).Returns(() => [.. episodeIndexes.Select(index => Episodes[index])]);
            _seasons.Add(season.Object);
            return season.Object;
        }

        /// <summary>Sets the links the undated AniDB anime has.</summary>
        public void UseEpisodeLinks(params IMetadataEpisodeCrossReference[] links)
            => CrossReferences
                .Setup(store => store.GetEpisodeLinksForSeries(UndatedAnimeID, It.IsAny<MetadataSource?>()))
                .Returns(links);

        public void Dispose()
        {
            Notifications.Dispose();
            _scope.Dispose();
        }

        /// <summary>
        ///   Resolves the Moq entities back from the keys the service stored them under, the way a
        ///   plugin's own metadata resolver does for its entities.
        /// </summary>
        /// <param name="id">The ID of the entity.</param>
        /// <returns>The entity, or <c>null</c> when the harness has none with that ID.</returns>
        private IMetadata? Resolve(MetadataGuid id)
            => id.EntityType switch
            {
                _ when id.EntityType == MetadataEntityType.Series => new[] { ProviderSeries, ShokoSeries, OrphanSeries }
                    .FirstOrDefault(entity => entity.ID.ID == id.ID),
                _ when id.EntityType == MetadataEntityType.Season => _seasons.FirstOrDefault(entity => entity.ID.ID == id.ID),
                _ when id.EntityType == MetadataEntityType.Episode => Episodes.Cast<IMetadata>()
                    .Concat(ShokoEpisodes)
                    .Append(OrphanEpisode)
                    .FirstOrDefault(entity => entity is IEpisode episode && episode.ID.ID == id.ID),
                _ => null,
            };
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
