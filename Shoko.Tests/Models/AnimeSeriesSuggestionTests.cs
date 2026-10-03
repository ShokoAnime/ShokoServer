using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Server.Models.Shoko.Embedded;
using Xunit;

namespace Shoko.Tests.Models;

/// <summary>
/// Covers <see cref="AnimeSeriesSuggestion"/>, which bases a linked series' suggestion on a Shoko
/// series and leaves the rest as the source gave it.
/// </summary>
public class AnimeSeriesSuggestionTests
{
    private static readonly MetadataGuid _anidbBase = new(MetadataSource.AniDB, MetadataEntityType.Series, "1");

    private static readonly MetadataGuid _anidbSuggested = new(MetadataSource.AniDB, MetadataEntityType.Series, "2");

    private static ISuggestedMetadata<ISeries, ISeries> Linked(ISeries suggested)
    {
        var suggestion = new Mock<ISuggestedMetadata<ISeries, ISeries>>();
        suggestion.SetupGet(s => s.BaseID).Returns(_anidbBase);
        suggestion.SetupGet(s => s.SuggestedID).Returns(_anidbSuggested);
        suggestion.SetupGet(s => s.Suggested).Returns(suggested);
        suggestion.SetupGet(s => s.Kind).Returns(SuggestionKind.Similar);
        suggestion.SetupGet(s => s.Order).Returns(3);
        suggestion.SetupGet(s => s.ApprovalVotes).Returns(30);
        suggestion.SetupGet(s => s.Votes).Returns(40);
        suggestion.SetupGet(s => s.Source).Returns(MetadataSource.AniDB);
        return suggestion.Object;
    }

    [Fact]
    public void TheBaseBecomesTheShokoSeriesAndTheRestIsPassedThrough()
    {
        var shoko = new Mock<IShokoSeries>();
        shoko.SetupGet(s => s.ID).Returns(new MetadataGuid(MetadataSource.Shoko, MetadataEntityType.Series, "7"));
        var suggested = Mock.Of<ISeries>();

        ISuggestedMetadata<ISeries, ISeries> wrapped = new AnimeSeriesSuggestion(Linked(suggested), shoko.Object);

        Assert.Equal(("shoko://series/7", "anidb://series/2"), (wrapped.BaseID.ToString(), wrapped.SuggestedID.ToString()));
        Assert.Same(shoko.Object, wrapped.Base);
        Assert.Same(suggested, wrapped.Suggested);
        Assert.Equal((SuggestionKind.Similar, 3, 30, 40, MetadataSource.AniDB), (wrapped.Kind, wrapped.Order, wrapped.ApprovalVotes, wrapped.Votes, wrapped.Source));
    }

    [Fact]
    public void WithoutAShokoSeriesTheSourcesBaseIDIsKept()
    {
        var wrapped = new AnimeSeriesSuggestion(Linked(Mock.Of<ISeries>()), null);

        Assert.Equal(_anidbBase, wrapped.BaseID);
        Assert.Null(wrapped.Base);
    }
}
