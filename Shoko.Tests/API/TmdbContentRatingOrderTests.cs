using System.Linq;
using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Server.API.v3.Helpers;
using Xunit;

namespace Shoko.Tests.API;

/// <summary>
/// Covers the order APIv3 gives a TMDB entry's content ratings: by country,
/// as upstream stored them, whatever order TMDB sent them in.
/// </summary>
public class TmdbContentRatingOrderTests
{
    private static IContentRating Rating(string country, string value)
    {
        var rating = new Mock<IContentRating>();
        rating.SetupGet(r => r.CountryCode).Returns(country);
        rating.SetupGet(r => r.Value).Returns(value);
        return rating.Object;
    }

    [Fact]
    public void RatingsComeByCountryAndKeepTheirOrderWithinOne()
    {
        var ratings = new[] { Rating("US", "TV-14"), Rating("DE", "12"), Rating("JP", "G"), Rating("DE", "16") };

        var ordered = TmdbCompatibility.ByCountry(ratings);

        Assert.Equal(["DE:12", "DE:16", "JP:G", "US:TV-14"], ordered.Select(rating => $"{rating.CountryCode}:{rating.Value}"));
    }
}
