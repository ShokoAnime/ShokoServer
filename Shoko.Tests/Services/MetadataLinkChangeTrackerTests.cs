using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Events;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

using LinkRowChange = Shoko.Server.Services.MetadataLinkChangeTracker.LinkRowChange;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers <see cref="MetadataLinkChangeTracker"/>: how the rows a write
/// changed become the links it changed, and when they are reported.
/// </summary>
public class MetadataLinkChangeTrackerTests
{
    #region Fixtures

    private static readonly MetadataSource Source = TestSources.Plugin;

    private static LinkRowChange Row(string? providerID, MatchRating? before, MatchRating? after, int? anidbEpisodeID = null)
        => new(
            anidbEpisodeID is null ? MetadataEntityType.Series : MetadataEntityType.Episode,
            Source,
            1,
            anidbEpisodeID,
            providerID is null ? null : new(Source, anidbEpisodeID is null ? MetadataEntityType.Series : MetadataEntityType.Episode, providerID),
            before,
            after
        );

    private static List<MetadataLinksChangedEventArgs> Listen(MetadataLinkChangeTracker tracker)
    {
        var raised = new List<MetadataLinksChangedEventArgs>();
        tracker.Changed += (_, eventArgs) => raised.Add(eventArgs);
        return raised;
    }

    #endregion

    #region Consolidating

    [Fact]
    public void EachKindOfChangeIsTold()
    {
        var changes = MetadataLinkChangeTracker.Consolidate(
        [
            Row("1", null, MatchRating.TitleMatches),
            Row("2", MatchRating.DateMatches, null),
            Row("3", MatchRating.TitleMatches, MatchRating.UserVerified),
            Row("4", MatchRating.TitleMatches, MatchRating.TitleMatches),
        ]);

        Assert.Equal(
            [
                (MetadataLinkChangeKind.Replaced, "1", "2", MatchRating.DateMatches, MatchRating.TitleMatches),
                (MetadataLinkChangeKind.RatingChanged, "3", null, MatchRating.TitleMatches, MatchRating.UserVerified),
            ],
            changes.Select(change => (change.Kind, change.ProviderID!.ID, change.PreviousProviderID?.ID, change.PreviousMatchRating, change.MatchRating))
        );
    }

    // A link is judged from before its first write to after its last, so one
    // added and removed again, or put back as it was, is no change.
    [Fact]
    public void ALinkIsJudgedAcrossEveryWriteToIt()
    {
        var changes = MetadataLinkChangeTracker.Consolidate(
        [
            Row("1", null, MatchRating.TitleMatches),
            Row("1", MatchRating.TitleMatches, null),
            Row("2", MatchRating.DateMatches, null),
            Row("2", null, MatchRating.DateMatches),
            Row("3", MatchRating.DateMatches, null),
            Row("3", null, MatchRating.UserVerified),
        ]);

        var change = Assert.Single(changes);
        Assert.Equal((MetadataLinkChangeKind.RatingChanged, "3", MatchRating.DateMatches, MatchRating.UserVerified),
            (change.Kind, change.ProviderID!.ID, change.PreviousMatchRating, change.MatchRating));
    }

    // Only a link removed and one added at the same place are a replacement,
    // a link to nothing included.
    [Fact]
    public void OnlyTheSamePlaceIsAReplacement()
    {
        var changes = MetadataLinkChangeTracker.Consolidate(
        [
            Row("11", MatchRating.DateMatches, null, anidbEpisodeID: 10),
            Row("1", null, MatchRating.TitleMatches),
            Row(null, null, MatchRating.UserVerified, anidbEpisodeID: 10),
        ]);

        Assert.Equal(
            [(MetadataLinkChangeKind.Replaced, null, "11", (int?)10), (MetadataLinkChangeKind.Added, "1", null, null)],
            changes.Select(change => (change.Kind, change.ProviderID?.ID, change.PreviousProviderID?.ID, change.AnidbEpisodeID))
        );
    }

    #endregion

    #region Reporting

    [Fact]
    public void AWriteOutsideAnOperationIsReportedAtOnce()
    {
        var tracker = new MetadataLinkChangeTracker();
        var raised = Listen(tracker);

        tracker.Record([Row("1", null, MatchRating.TitleMatches)]);
        tracker.Record([Row("2", null, MatchRating.TitleMatches)]);

        Assert.Equal(2, raised.Count);
        Assert.All(raised, eventArgs => Assert.Equal(MetadataLinkChangeReason.Other, eventArgs.Reason));
    }

    [Fact]
    public async Task AnOperationReportsEverythingOnceItEnds()
    {
        var tracker = new MetadataLinkChangeTracker();
        var raised = Listen(tracker);

        using (tracker.Begin(MetadataLinkChangeReason.Import))
        {
            tracker.Record([Row("1", null, MatchRating.TitleMatches)]);
            await Task.Yield();
            await Task.Run(() => tracker.Record([Row("2", null, MatchRating.TitleMatches)]), TestContext.Current.CancellationToken);

            // One begun inside joins it, whatever reason it gives.
            using (tracker.Begin(MetadataLinkChangeReason.Verify))
                tracker.Record([Row("3", null, MatchRating.TitleMatches)]);

            Assert.Empty(raised);
        }

        var eventArgs = Assert.Single(raised);
        Assert.Equal(MetadataLinkChangeReason.Import, eventArgs.Reason);
        Assert.Equal(["1", "2", "3"], eventArgs.Changes.Select(change => change.ProviderID!.ID));
    }

    // An operation giving a reason inside one begun without is reported on
    // its own, and the outer one goes on collecting once it ends.
    [Fact]
    public void AnInnerReasonIsReportedOnItsOwn()
    {
        var tracker = new MetadataLinkChangeTracker();
        var raised = Listen(tracker);

        using (tracker.UseDefaultReason(MetadataLinkChangeReason.Manual))
        using (tracker.Begin())
        {
            tracker.Record([Row("1", null, MatchRating.TitleMatches)]);
            using (tracker.Begin(MetadataLinkChangeReason.Verify))
            {
                tracker.Record([Row("2", null, MatchRating.TitleMatches)]);
                using (tracker.Begin(MetadataLinkChangeReason.Import))
                    tracker.Record([Row("3", null, MatchRating.TitleMatches)]);
            }

            Assert.Single(raised);
            tracker.Record([Row("4", null, MatchRating.TitleMatches)]);
        }

        Assert.Equal(
            [(MetadataLinkChangeReason.Verify, new[] { "2", "3" }), (MetadataLinkChangeReason.Manual, new[] { "1", "4" })],
            raised.Select(eventArgs => (eventArgs.Reason, eventArgs.Changes.Select(change => change.ProviderID!.ID).ToArray()))
        );
    }

    [Fact]
    public void TheFlowsReasonIsUsedWhereNoOperationGivesOne()
    {
        var tracker = new MetadataLinkChangeTracker();
        var raised = Listen(tracker);

        using (tracker.UseDefaultReason(MetadataLinkChangeReason.Manual))
        {
            tracker.Record([Row("1", null, MatchRating.TitleMatches)]);
            using (tracker.Begin())
                tracker.Record([Row("2", null, MatchRating.TitleMatches)]);
            using (tracker.Begin(MetadataLinkChangeReason.Verify))
                tracker.Record([Row("3", null, MatchRating.TitleMatches)]);
        }

        tracker.Record([Row("4", null, MatchRating.TitleMatches)]);

        Assert.Equal(
            [MetadataLinkChangeReason.Manual, MetadataLinkChangeReason.Manual, MetadataLinkChangeReason.Verify, MetadataLinkChangeReason.Other],
            raised.Select(eventArgs => eventArgs.Reason)
        );
    }

    // A copy of the flow made during a call, as a timer or task begun in it
    // keeps, no longer reports as the call once the call has ended.
    [Fact]
    public void ACopiedFlowOutlivesNeitherTheCallNorItsReason()
    {
        var tracker = new MetadataLinkChangeTracker();
        var raised = Listen(tracker);
        ExecutionContext? copied;

        using (tracker.UseDefaultReason(MetadataLinkChangeReason.Manual))
        using (tracker.Begin())
            copied = ExecutionContext.Capture();

        ExecutionContext.Run(copied!, _ =>
        {
            tracker.Record([Row("1", null, MatchRating.TitleMatches)]);
            using (tracker.Begin())
                tracker.Record([Row("2", null, MatchRating.TitleMatches)]);
        }, null);

        Assert.Equal(
            [(MetadataLinkChangeReason.Other, "1"), (MetadataLinkChangeReason.Other, "2")],
            raised.Select(eventArgs => (eventArgs.Reason, Assert.Single(eventArgs.Changes).ProviderID!.ID))
        );
    }

    [Fact]
    public void AnOperationThatChangedNothingRaisesNothing()
    {
        var tracker = new MetadataLinkChangeTracker();
        var raised = Listen(tracker);

        using (tracker.Begin(MetadataLinkChangeReason.AutoLink))
            tracker.Record([Row("1", MatchRating.TitleMatches, MatchRating.TitleMatches)]);

        Assert.Empty(raised);
    }

    // TMDB's own code writes through the store's internal calls, which are
    // reported like any other write.
    [Fact]
    public void TheServersOwnWritesAreReported()
    {
        var tracker = new MetadataLinkChangeTracker();
        var raised = Listen(tracker);
        var links = new WritableLinkStore(tracker);
        var show = new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Series, "5");

        links.Store.AddSeriesLink(MetadataSource.TMDB, 1, show, MatchRating.TitleMatches);
        links.Store.AddSeriesLink(MetadataSource.TMDB, 1, show, MatchRating.UserVerified);
        links.Store.AddEpisodeLink(MetadataSource.TMDB, 1, 10, new(MetadataSource.TMDB, MetadataEntityType.Episode, "55"), show, MatchRating.DateMatches);
        links.Store.RemoveEpisodeLink(MetadataSource.TMDB, 1, 10, new(MetadataSource.TMDB, MetadataEntityType.Episode, "55"));
        links.Store.RemoveSeriesLink(MetadataSource.TMDB, 1, show);

        Assert.Equal(
            [
                (MetadataLinkChangeKind.Added, MetadataEntityType.Series),
                (MetadataLinkChangeKind.RatingChanged, MetadataEntityType.Series),
                (MetadataLinkChangeKind.Added, MetadataEntityType.Episode),
                (MetadataLinkChangeKind.Removed, MetadataEntityType.Episode),
                (MetadataLinkChangeKind.Removed, MetadataEntityType.Series),
            ],
            raised.Select(eventArgs => Assert.Single(eventArgs.Changes)).Select(change => (change.Kind, change.EntityType))
        );
    }

    // A bulk write in one batch, as an AniDB refresh dropping episodes
    // makes, is one change per batch.
    [Fact]
    public void ABatchedBulkWriteIsOneChange()
    {
        var tracker = new MetadataLinkChangeTracker();
        var links = new WritableLinkStore(tracker);
        var show = new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Series, "5");
        links.Store.AddSeriesLink(MetadataSource.TMDB, 1, show, MatchRating.TitleMatches);
        var raised = Listen(tracker);

        using (links.Store.BeginChanges())
        {
            foreach (var (anidbEpisodeID, episodeID) in (ReadOnlySpan<(int, string)>)[(10, "55"), (11, "56"), (12, "57")])
                links.Store.AddEpisodeLink(MetadataSource.TMDB, 1, anidbEpisodeID, new(MetadataSource.TMDB, MetadataEntityType.Episode, episodeID), show, MatchRating.UserVerified);
        }

        using (links.Store.BeginChanges())
        {
            foreach (var link in links.Episodes.GetByAnidbAnimeID(1).ToList())
                links.Store.RemoveEpisodeLink(link.Source, link.AnidbAnimeID, link.AnidbEpisodeID, ((IMetadataCrossReference)link).ProviderID);
        }

        Assert.Equal(
            [
                [MetadataLinkChangeKind.Added, MetadataLinkChangeKind.Added, MetadataLinkChangeKind.Added],
                [MetadataLinkChangeKind.Removed, MetadataLinkChangeKind.Removed, MetadataLinkChangeKind.Removed],
            ],
            raised.Select(eventArgs => eventArgs.Changes.Select(change => change.Kind).ToList())
        );
    }

    [Fact]
    public void AFailingHandlerDoesNotKeepTheOthersFromIt()
    {
        var tracker = new MetadataLinkChangeTracker();
        tracker.Changed += (_, _) => throw new InvalidOperationException("A handler failed.");
        var raised = Listen(tracker);

        tracker.Record([Row("1", null, MatchRating.TitleMatches)]);

        Assert.Single(raised);
    }

    #endregion
}
