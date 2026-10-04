using System.ComponentModel;
using Newtonsoft.Json;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.API.Resolvers;
using Shoko.Server.API.v3.Models.Common;
using Xunit;

namespace Shoko.Tests.API;

/// <summary>
/// Covers the query form of <see cref="SeasonWithYear"/>, <c>{Season} {Year}</c>,
/// which the AniDB anime list's <c>seasons</c> filter binds through its type
/// converter, and that the converter leaves the JSON shape alone.
/// </summary>
public class SeasonWithYearTests
{
    [Theory]
    [InlineData("Fall 2026", 2026, YearlySeason.Fall)]
    [InlineData("winter 2027", 2027, YearlySeason.Winter)]
    [InlineData("  Spring   2015 ", 2015, YearlySeason.Spring)]
    [InlineData("Autumn 2026", 2026, YearlySeason.Fall)]
    public void TryParse_ReadsTheQueryForm(string text, int year, YearlySeason season)
    {
        Assert.True(SeasonWithYear.TryParse(text, out var parsed));
        Assert.Equal(new SeasonWithYear(year, season), parsed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Fall")]
    [InlineData("2026 Fall")]
    [InlineData("Fall2026")]
    [InlineData("3 2026")]
    [InlineData("Monsoon 2026")]
    [InlineData("Fall 26x")]
    [InlineData("Fall 1800")]
    [InlineData("Fall 2026 extra")]
    public void TryParse_RefusesAnythingElse(string text)
        => Assert.False(SeasonWithYear.TryParse(text, out _));

    [Fact]
    public void TypeConverter_RoundTripsTheQueryForm()
    {
        var converter = TypeDescriptor.GetConverter(typeof(SeasonWithYear));

        Assert.Equal(new SeasonWithYear(2026, YearlySeason.Fall), converter.ConvertFromString("Fall 2026"));
        Assert.Equal("Fall 2026", converter.ConvertToString(new SeasonWithYear(2026, YearlySeason.Fall)));
        Assert.Throws<System.FormatException>(() => converter.ConvertFromString("2026-Fall"));
    }

    [Fact]
    public void Json_StaysAnObject()
    {
        var settings = new JsonSerializerSettings { ContractResolver = new ApiContractResolver() };

        var json = JsonConvert.SerializeObject(new SeasonWithYear(2026, YearlySeason.Fall), settings);

        Assert.Equal("""{"Year":2026,"AnimeSeason":"Fall"}""", json);
    }
}
