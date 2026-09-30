using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Events;
using Shoko.Server.API.ActionFilters;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.API;

/// <summary>
/// Covers <see cref="ManualLinkChangeFilter"/>: an API call writing several
/// links is reported as one change.
/// </summary>
public class ManualLinkChangeFilterTests
{
    #region Fixtures

    private static readonly MetadataGuid Show = new(MetadataSource.TMDB, MetadataEntityType.Series, "5");

    private static MetadataGuid Episode(int id)
        => new(MetadataSource.TMDB, MetadataEntityType.Episode, id.ToString());

    /// <summary>
    /// Runs the filter around an action.
    /// </summary>
    /// <param name="tracker">The tracker the filter reports to.</param>
    /// <param name="action">The action.</param>
    private static Task Run(MetadataLinkChangeTracker tracker, Action action)
    {
        var actionContext = new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor());
        var executing = new ActionExecutingContext(actionContext, [], new Dictionary<string, object?>(), new object());
        return new ManualLinkChangeFilter(tracker).OnActionExecutionAsync(executing, async () =>
        {
            await Task.Yield();
            action();
            return new ActionExecutedContext(actionContext, [], new object());
        });
    }

    private static List<MetadataLinksChangedEventArgs> Listen(MetadataLinkChangeTracker tracker)
    {
        var raised = new List<MetadataLinksChangedEventArgs>();
        tracker.Changed += (_, eventArgs) => raised.Add(eventArgs);
        return raised;
    }

    #endregion

    #region Batching

    // Episodes moved to another entry are told as replaced.
    [Fact]
    public async Task AMultiItemMappingCallIsOneChange()
    {
        var tracker = new MetadataLinkChangeTracker();
        var links = new WritableLinkStore(tracker);
        links.Store.AddSeriesLink(MetadataSource.TMDB, 1, Show, MatchRating.UserVerified);
        links.Store.AddEpisodeLink(MetadataSource.TMDB, 1, 10, Episode(55), Show, MatchRating.UserVerified);
        links.Store.AddEpisodeLink(MetadataSource.TMDB, 1, 11, Episode(56), Show, MatchRating.UserVerified);
        var raised = Listen(tracker);

        await Run(tracker, () =>
        {
            links.Store.RemoveEpisodeLink(MetadataSource.TMDB, 1, 10, Episode(55));
            links.Store.RemoveEpisodeLink(MetadataSource.TMDB, 1, 11, Episode(56));
            links.Store.AddEpisodeLink(MetadataSource.TMDB, 1, 10, Episode(57), Show, MatchRating.UserVerified);
            links.Store.AddEpisodeLink(MetadataSource.TMDB, 1, 11, Episode(58), Show, MatchRating.UserVerified);
            links.Store.AddEpisodeLink(MetadataSource.TMDB, 1, 12, Episode(59), Show, MatchRating.UserVerified);
        });

        var eventArgs = Assert.Single(raised);
        Assert.Equal(MetadataLinkChangeReason.Manual, eventArgs.Reason);
        Assert.Equal(
            [
                (MetadataLinkChangeKind.Replaced, "57", "55"),
                (MetadataLinkChangeKind.Replaced, "58", "56"),
                (MetadataLinkChangeKind.Added, "59", (string?)null),
            ],
            eventArgs.Changes.Select(change => (change.Kind, change.ProviderID!.ID, change.PreviousProviderID?.ID))
        );
    }

    // What gives its own reason inside the call, as a verify or an import
    // does, is reported on its own.
    [Fact]
    public async Task AReasonGivenInsideTheCallIsReportedOnItsOwn()
    {
        var tracker = new MetadataLinkChangeTracker();
        var links = new WritableLinkStore(tracker);
        var raised = Listen(tracker);

        await Run(tracker, () =>
        {
            using (tracker.Begin(MetadataLinkChangeReason.Verify))
                links.Store.AddSeriesLink(MetadataSource.TMDB, 1, Show, MatchRating.UserVerified);
            links.Store.AddEpisodeLink(MetadataSource.TMDB, 1, 10, Episode(55), Show, MatchRating.UserVerified);
        });

        Assert.Equal(
            [(MetadataLinkChangeReason.Verify, MetadataEntityType.Series), (MetadataLinkChangeReason.Manual, MetadataEntityType.Episode)],
            raised.Select(eventArgs => (eventArgs.Reason, Assert.Single(eventArgs.Changes).EntityType))
        );
    }

    #endregion
}
