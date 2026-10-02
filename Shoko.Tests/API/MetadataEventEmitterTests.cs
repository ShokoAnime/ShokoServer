using System.Linq;
using System.Threading.Tasks;
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
using Shoko.Tests.Infrastructure;
using Xunit;
using static Shoko.Tests.Infrastructure.TestViewers;

namespace Shoko.Tests.API;

/// <summary>
/// Covers the link events <see cref="MetadataEventEmitter"/> forwards to the
/// metadata feed.
/// </summary>
public class MetadataEventEmitterTests
{
    [Fact]
    public async Task LinksChanged_IsSentToTheMetadataFeed()
    {
        var harness = await Harness.Create();
        var series = new Mock<IShokoSeries>();
        series.Setup(s => s.LocalID).Returns(42);
        harness.Metadata.Setup(m => m.GetShokoSeriesByAnidbID(VisibleAnimeID)).Returns(series.Object);

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
                    AnidbAnimeID = VisibleAnimeID,
                    ProviderID = new(MetadataSource.TMDB, MetadataEntityType.Series, "2"),
                    PreviousProviderID = new(MetadataSource.TMDB, MetadataEntityType.Series, "1"),
                    MatchRating = MatchRating.TitleMatches,
                    PreviousMatchRating = MatchRating.UserVerified,
                },
            ],
        });

        var message = Assert.Single(harness.Hub.Sent);
        Assert.Equal([RestrictedConnection, UnrestrictedConnection], message.ConnectionIDs.Order());
        var model = Assert.IsType<MetadataLinksChangedSignalRModel>(Assert.Single(message.Args));
        Assert.Equal([42], model.ShokoSeriesIDs);
        var json = JObject.FromObject(model);
        Assert.Equal("ForcedResearch", json["Reason"]?.ToString());
        var change = Assert.Single(json["Changes"]!);
        Assert.Equal(("2", "1"), (change["ID"]?.ToString(), change["PreviousID"]?.ToString()));
    }

    [Fact]
    public async Task LinksChanged_IsNarrowedToTheAnimeTheUserMaySee()
    {
        var harness = await Harness.Create();

        harness.Linking.Raise(l => l.LinksChanged += null, new MetadataLinksChangedEventArgs
        {
            Reason = MetadataLinkChangeReason.ForcedResearch,
            Changes =
            [
                new() { Kind = MetadataLinkChangeKind.Added, Source = MetadataSource.TMDB, EntityType = MetadataEntityType.Series, AnidbAnimeID = HiddenAnimeID },
                new() { Kind = MetadataLinkChangeKind.Added, Source = MetadataSource.TMDB, EntityType = MetadataEntityType.Series, AnidbAnimeID = VisibleAnimeID },
            ],
        });

        int[] AnimeSentTo(string connectionID)
            => [.. harness.Hub.Sent.Single(message => message.ConnectionIDs.Contains(connectionID)).Args
                .OfType<MetadataLinksChangedSignalRModel>().Single().Changes.Select(change => change.AnidbAnimeID)];
        Assert.Equal([VisibleAnimeID], AnimeSentTo(RestrictedConnection));
        Assert.Equal([HiddenAnimeID, VisibleAnimeID], AnimeSentTo(UnrestrictedConnection));
    }

    [Fact]
    public async Task Dispose_StopsForwarding()
    {
        var harness = await Harness.Create();

        harness.Emitter.Dispose();
        harness.Linking.Raise(l => l.LinksChanged += null, new MetadataLinksChangedEventArgs
        {
            Reason = MetadataLinkChangeReason.ForcedResearch,
            Changes = [new() { Kind = MetadataLinkChangeKind.Added, Source = MetadataSource.TMDB, EntityType = MetadataEntityType.Series, AnidbAnimeID = VisibleAnimeID }],
        });

        Assert.Empty(harness.Hub.Sent);
    }

    private sealed class Harness
    {
        public RecordingHub Hub { get; } = new();

        public Mock<IMetadataService> Metadata { get; } = new();

        public Mock<IMetadataLinkingService> Linking { get; } = new();

        public MetadataEventEmitter Emitter { get; }

        private Harness()
            => Emitter = new(Hub.Typed, Metadata.Object, Linking.Object, AnimeRepository(), NullLogger<MetadataEventEmitter>.Instance);

        public static async Task<Harness> Create()
        {
            var harness = new Harness();
            await harness.Emitter.ConnectAsync(RestrictedConnection, Restricted());
            await harness.Emitter.ConnectAsync(UnrestrictedConnection, Unrestricted());
            return harness;
        }
    }
}
