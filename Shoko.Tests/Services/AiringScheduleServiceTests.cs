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
        var one = harness.Service.AddOrUpdateAiring(harness.Primary, first, new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = harness.Air(1, 15) });
        var two = harness.Service.AddOrUpdateAiring(harness.Primary, second, new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = harness.Air(1, 15) });

        Assert.Throws<ArgumentException>(() => harness.Service.LinkAirings(harness.Primary, [one, two]));
    }

    [Fact]
    public void RemoveAiring_PromotesTheNextSmallestWhenTheHeadGoes()
    {
        using var harness = new Harness();
        var schedule = harness.Service.AddOrUpdateSchedule(harness.Primary, harness.ScheduleData());
        var airings = harness.Service.SetAirings(harness.Primary, schedule, [
            new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = harness.Air(1, 15) },
            new EpisodeAiringData() { Episode = harness.Episodes[1], AiredAt = harness.Air(1, 15) },
            new EpisodeAiringData() { Episode = harness.Episodes[2], AiredAt = harness.Air(1, 15) },
        ]);
        var linked = harness.Service.LinkAirings(harness.Primary, airings);

        harness.Service.RemoveAiring(harness.Primary, linked[0]);

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
    public void SetAirings_RefusesAnAiringOlderThanTheRetentionWindow()
    {
        using var harness = new Harness();
        var schedule = harness.Service.AddOrUpdateSchedule(harness.Primary, harness.ScheduleData());

        Assert.Throws<AiringScheduleValidationException>(() => harness.Service.SetAirings(harness.Primary, schedule, [
            new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = DateTime.UtcNow.AddYears(-3) },
        ]));

        harness.Settings.AutoCleanup = false;
        Assert.Single(harness.Service.SetAirings(harness.Primary, schedule, [
            new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = DateTime.UtcNow.AddYears(-3) },
        ]));
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
    public void Tick_RaisesAnAiringInTheHorizonOnceAndOnlyOnce()
    {
        using var harness = new Harness();
        var now = Harness.Minute(DateTime.UtcNow);
        var tokyo = harness.Service.FindOrRegisterChannel("TOKYO MX", AiringChannelType.Television);
        harness.Schedule(harness.Primary, "mx", tokyo.ID, [new AiringTrackData(AiringKind.Original, "ja")], (0, now.AddMinutes(5)));
        var raised = new List<EpisodeAiredEventArgs>();
        harness.Service.EpisodeAired += (_, args) => raised.Add(args);

        // The first tick only picks the watermark up; nothing has come due yet.
        harness.Notifications.Tick(now);
        Assert.Empty(raised);

        harness.Notifications.Tick(now.AddMinutes(5));
        var aired = Assert.Single(raised);
        Assert.Equal(now.AddMinutes(5), aired.AiredAt);
        Assert.False(aired.Airing.IsEstimated);

        // The watermark is past it now, so a later tick — and the rebuild that
        // comes with it — never hands the same slot out twice.
        harness.Notifications.Tick(now.AddMinutes(6));
        Assert.Single(raised);
    }

    [Fact]
    public void Tick_RaisesOneEventPerAiringRatherThanPerEpisode()
    {
        using var harness = new Harness();
        var now = Harness.Minute(DateTime.UtcNow);
        var tokyo = harness.Service.FindOrRegisterChannel("TOKYO MX", AiringChannelType.Television);
        var bs11 = harness.Service.FindOrRegisterChannel("BS11", AiringChannelType.Television);
        var tracks = new[] { new AiringTrackData(AiringKind.Original, "ja") };
        harness.Schedule(harness.Primary, "mx", tokyo.ID, tracks, (0, now.AddMinutes(5)));
        harness.Schedule(harness.Primary, "bs11", bs11.ID, tracks, (0, now.AddMinutes(5)));
        var raised = new List<EpisodeAiredEventArgs>();
        harness.Service.EpisodeAired += (_, args) => raised.Add(args);

        harness.Notifications.Tick(now);
        harness.Notifications.Tick(now.AddMinutes(5));

        // One episode, two stations, two events. Whoever wants "this episode
        // aired" de-duplicates by episode themselves.
        Assert.Equal(2, raised.Count);
        Assert.Equal(2, raised.Select(args => args.Airing.ID).Distinct().Count());
        Assert.Equal(new HashSet<Guid> { tokyo.ID, bs11.ID }, raised.Select(args => args.Airing.Channel!.ID).ToHashSet());
    }

    [Fact]
    public void Tick_RaisesAnEstimateAndFlagsItAsOne()
    {
        using var harness = new Harness();
        var now = Harness.Minute(DateTime.UtcNow);
        var tokyo = harness.Service.FindOrRegisterChannel("TOKYO MX", AiringChannelType.Television);
        // Two real airings a fixed distance from their AniDB dates are what the
        // schedule learns its slot from, and the distance is picked so the
        // fourth episode's estimate lands five minutes out.
        var offset = now.AddMinutes(5) - harness.Air(22, 0, 0);
        harness.Schedule(
            harness.Primary,
            "mx",
            tokyo.ID,
            [new AiringTrackData(AiringKind.Original, "ja")],
            (0, harness.Air(1, 0, 0) + offset),
            (1, harness.Air(8, 0, 0) + offset)
        );
        var raised = new List<EpisodeAiredEventArgs>();
        harness.Service.EpisodeAired += (_, args) => raised.Add(args);

        harness.Notifications.Tick(now);
        harness.Notifications.Tick(now.AddMinutes(5));

        var aired = Assert.Single(raised);
        Assert.True(aired.Airing.IsEstimated);
        Assert.Equal(now.AddMinutes(5), aired.AiredAt);
    }

    [Fact]
    public void Tick_SkipsForwardInsteadOfReplayingWhatItMissed()
    {
        using var harness = new Harness();
        var now = Harness.Minute(DateTime.UtcNow);
        harness.SeedWatermark(now.AddHours(-3));
        var tokyo = harness.Service.FindOrRegisterChannel("TOKYO MX", AiringChannelType.Television);
        harness.Schedule(harness.Primary, "mx", tokyo.ID, [new AiringTrackData(AiringKind.Original, "ja")], (0, now.AddMinutes(-30)));
        var raised = new List<EpisodeAiredEventArgs>();
        harness.Service.EpisodeAired += (_, args) => raised.Add(args);

        harness.Notifications.Tick(now);

        // The slot passed while the server was down. An hours-old prediction is
        // worse than none, so it is stepped over rather than announced late.
        Assert.Empty(raised);
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
        var raised = new List<EpisodeAiredEventArgs>();
        harness.Service.EpisodeAired += (_, args) => raised.Add(args);

        harness.Notifications.Tick(now);
        Assert.Single(harness.Notifications.Horizon);

        // The provider moves the slot back by forty minutes, well inside the
        // horizon and well before the periodic rebuild would have noticed.
        harness.Service.SetAirings(harness.Primary, schedule, [
            new EpisodeAiringData() { Episode = harness.Episodes[0], AiredAt = now.AddMinutes(50) },
        ]);

        harness.Notifications.Tick(now.AddMinutes(10));
        Assert.Empty(raised);

        harness.Notifications.Tick(now.AddMinutes(50));
        var aired = Assert.Single(raised);
        Assert.Equal(now.AddMinutes(50), aired.AiredAt);
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
            bool shokoEntities = false
        )
            => new()
            {
                Series = shokoEntities ? ShokoSeries : ProviderSeries,
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
                    DataEntityType.Series => harness.ProviderSeries.ID.ToString() == id
                        ? harness.ProviderSeries
                        : harness.ShokoSeries.ID.ToString() == id ? harness.ShokoSeries : null,
                    DataEntityType.Episode => harness.Episodes.Cast<IMetadata>()
                        .Concat(harness.ShokoEpisodes)
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
