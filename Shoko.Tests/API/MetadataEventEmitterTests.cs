using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Events;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Server.API.SignalR.Aggregate;
using Shoko.Server.API.SignalR.Models;
using Xunit;

namespace Shoko.Tests.API;

/// <summary>
/// Covers the link events <see cref="MetadataEventEmitter"/> forwards to the
/// metadata feed.
/// </summary>
public class MetadataEventEmitterTests
{
    [Fact]
    public void LinksChanged_IsSentToTheMetadataFeed()
    {
        var harness = new Harness();
        var series = new Mock<IShokoSeries>();
        series.Setup(s => s.LocalID).Returns(42);
        harness.Metadata.Setup(m => m.GetShokoSeriesByAnidbID(7)).Returns(series.Object);

        harness.Linking.Raise(l => l.LinksChanged += null, new MetadataLinksChangedEventArgs
        {
            Reason = MetadataLinkChangeReason.ForcedResearch,
            Changes =
            [
                new()
                {
                    Kind = MetadataLinkChangeKind.Replaced,
                    Source = MetadataSource.TMDB,
                    EntityType = MetadataEntityType.Series,
                    AnidbAnimeID = 7,
                    ProviderID = new(MetadataSource.TMDB, MetadataEntityType.Series, "2"),
                    PreviousProviderID = new(MetadataSource.TMDB, MetadataEntityType.Series, "1"),
                    MatchRating = MatchRating.TitleMatches,
                    PreviousMatchRating = MatchRating.UserVerified,
                },
            ],
        });

        var (_, args) = Assert.Single(harness.GroupSent);
        var model = Assert.IsType<MetadataLinksChangedSignalRModel>(Assert.Single(args));
        Assert.Equal([42], model.ShokoSeriesIDs);
        var json = JObject.FromObject(model);
        Assert.Equal("ForcedResearch", json["Reason"]?.ToString());
        var change = Assert.Single(json["Changes"]!);
        Assert.Equal(("2", "1"), (change["ID"]?.ToString(), change["PreviousID"]?.ToString()));
    }

    [Fact]
    public void Dispose_StopsForwarding()
    {
        var harness = new Harness();

        harness.Emitter.Dispose();
        harness.Linking.Raise(l => l.LinksChanged += null, new MetadataLinksChangedEventArgs
        {
            Reason = MetadataLinkChangeReason.ForcedResearch,
            Changes = [new() { Kind = MetadataLinkChangeKind.Added, Source = MetadataSource.TMDB, EntityType = MetadataEntityType.Series, AnidbAnimeID = 7 }],
        });

        Assert.Empty(harness.GroupSent);
    }

    private sealed class Harness
    {
        public Mock<IMetadataService> Metadata { get; } = new();

        public Mock<IMetadataLinkingService> Linking { get; } = new();

        public List<(string Method, object?[] Args)> GroupSent { get; } = [];

        public MetadataEventEmitter Emitter { get; }

        public Harness()
        {
            var group = new Mock<IClientProxy>();
            group.Setup(proxy => proxy.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
                .Callback((string method, object?[] args, CancellationToken _) => GroupSent.Add((method, args)))
                .Returns(Task.CompletedTask);
            var clients = new Mock<IHubClients>();
            clients.Setup(c => c.Group(It.IsAny<string>())).Returns(group.Object);
            var hub = new Mock<IHubContext<AggregateHub>>();
            hub.Setup(h => h.Clients).Returns(clients.Object);

            Emitter = new(hub.Object, Metadata.Object, Linking.Object, NullLogger<MetadataEventEmitter>.Instance);
        }
    }
}
