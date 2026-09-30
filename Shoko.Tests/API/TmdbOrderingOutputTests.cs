using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.API.v3.Models.TMDB;
using Shoko.Server.Models.TMDB;
using Xunit;

namespace Shoko.Tests.API;

/// <summary>
/// Covers the APIv3 shape of a TMDB show's orderings now that the episode
/// groups carry the generic ordering type and the choice is kept by the core:
/// the IDs, the type names and the flags come out as before.
/// </summary>
public class TmdbOrderingOutputTests
{
    private static TMDB_Show Show()
        => new(5) { EpisodeCount = 24, HiddenEpisodeCount = 2, SeasonCount = 2 };

    private static TMDB_AlternateOrdering Group()
        => new("5f0c1a2b3c4d5e6f7a8b9c0d")
        {
            TmdbShowID = 5,
            EnglishTitle = "DVD Order",
            Type = OrderingType.DVD,
            EpisodeCount = 24,
            HiddenEpisodeCount = 1,
            SeasonCount = 4,
        };

    [Fact]
    public void TheDefaultOrderingIsNamedByTheShowAndPreferredUntilAnotherIsChosen()
    {
        var json = JObject.FromObject(new TmdbShow.OrderingInformation(Show(), null));

        Assert.Equal("5", json[nameof(TmdbShow.OrderingInformation.OrderingID)]?.Value<string>());
        Assert.Equal("Seasons", json[nameof(TmdbShow.OrderingInformation.OrderingName)]?.Value<string>());
        Assert.False(json.ContainsKey(nameof(TmdbShow.OrderingInformation.OrderingType)));
        Assert.True(json[nameof(TmdbShow.OrderingInformation.IsDefault)]?.Value<bool>());
        Assert.True(json[nameof(TmdbShow.OrderingInformation.IsPreferred)]?.Value<bool>());
        Assert.True(json[nameof(TmdbShow.OrderingInformation.InUse)]?.Value<bool>());
    }

    [Fact]
    public void AnEpisodeGroupKeepsItsCollectionIDAndItsTypeName()
    {
        var group = Group();

        var json = JObject.FromObject(new TmdbShow.OrderingInformation(Show(), group, group));

        Assert.Equal("5f0c1a2b3c4d5e6f7a8b9c0d", json[nameof(TmdbShow.OrderingInformation.OrderingID)]?.Value<string>());
        Assert.Equal("DVD", json[nameof(TmdbShow.OrderingInformation.OrderingType)]?.Value<string>());
        Assert.False(json[nameof(TmdbShow.OrderingInformation.IsDefault)]?.Value<bool>());
        Assert.False(json[nameof(TmdbShow.OrderingInformation.IsPreferred)]?.Value<bool>());
        Assert.True(json[nameof(TmdbShow.OrderingInformation.InUse)]?.Value<bool>());
    }
}
