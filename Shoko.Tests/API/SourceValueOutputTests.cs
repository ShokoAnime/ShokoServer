using System;
using Moq;
using Shoko.Abstractions.Filtering.Expressions;
using Shoko.Abstractions.Filtering.Expressions.Info;
using Shoko.Abstractions.Filtering.Expressions.Selectors.NumberSelectors;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.ImageManagement;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Filters;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.API;

/// <summary>
/// Pins the APIv3 fields that are plain strings filled from a
/// <see cref="MetadataSource"/>, which have to spell the source as the old
/// enum did, just like the fields typed as a source.
/// </summary>
public class SourceValueOutputTests
{
    #region Filter Parameters

    [Theory]
    [InlineData(typeof(HasSourceLinkExpression))]
    [InlineData(typeof(MissingSourceLinkExpression))]
    [InlineData(typeof(HasAutomaticSourceLinkExpression))]
    [InlineData(typeof(AutomaticSourceLinksSelector))]
    [InlineData(typeof(UserVerifiedSourceLinksSelector))]
    public void TheSourceFilterParameters_AreOldSpellings(Type filterType)
    {
        var anilist = TestSources.AniList;
        var plugin = TestSources.Plugin;

        var parameters = new Filter.FilterExpressionHelp(ExpressionDiscovery.GetExpressionHelp(filterType)!).PossibleParameters;

        Assert.NotNull(parameters);
        Assert.Contains("TMDB", parameters);
        Assert.Contains("AniList", parameters);
        Assert.Contains(plugin.Value, parameters);
        Assert.DoesNotContain(MetadataSource.TMDB.Value, parameters);
        Assert.DoesNotContain(anilist.Value, parameters);
        Assert.All(parameters, parameter => Assert.True(MetadataSource.TryGet(parameter, out _), parameter));
    }

    [Fact]
    public void OtherFilterParameters_AreLeftAlone()
    {
        var help = new Mock<IFilterExpressionHelp>();
        help.SetupGet(h => h.InternalType).Returns(typeof(HasTagExpression));
        help.SetupGet(h => h.PossibleParameters).Returns(["tmdb", "anidb"]);

        var parameters = new Filter.FilterExpressionHelp(help.Object).PossibleParameters;

        Assert.Equal(["tmdb", "anidb"], parameters!);
    }

    #endregion

    #region Image Ratings

    [Fact]
    public void AnImagesCommunityRating_NamesTheSourceAsTheOldEnumDid()
    {
        var image = new Mock<IImage>(MockBehavior.Loose);
        image.SetupGet(i => i.Source).Returns(MetadataSource.TMDB);
        image.SetupGet(i => i.HasRating).Returns(true);
        image.SetupGet(i => i.Rating).Returns(7.5);
        image.SetupGet(i => i.RatingVotes).Returns(12);

        var model = new Image(image.Object);

        Assert.NotNull(model.CommunityRating);
        Assert.Equal("TMDB", model.CommunityRating.Source);
    }

    [Fact]
    public void AnImageCrossReferencesCommunityRating_NamesTheSourceAsTheOldEnumDid()
    {
        var xref = new Mock<IImageCrossReference>(MockBehavior.Loose);
        xref.SetupGet(x => x.Source).Returns(MetadataSource.TMDB);
        xref.SetupGet(x => x.ImageSource).Returns(MetadataSource.TMDB);
        xref.SetupGet(x => x.EntityID).Returns(new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Series, "1"));
        xref.SetupGet(x => x.HasRating).Returns(true);
        xref.SetupGet(x => x.Rating).Returns(7.5);
        xref.SetupGet(x => x.RatingVotes).Returns(12);

        var model = new ImageCrossReference(xref.Object);

        Assert.NotNull(model.CommunityRating);
        Assert.Equal("TMDB", model.CommunityRating.Source);
    }

    #endregion
}
