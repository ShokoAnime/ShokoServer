using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.API.v3.Helpers;
using Xunit;

namespace Shoko.Tests.API;

/// <summary>
/// Covers <see cref="ModelHelper.GetEpisodeNumberAndTypeFromInput"/>, which parses the
/// type-prefixed episode identifiers ("S3", "C1", …) that the v3 API accepts in range parameters.
/// A misparse silently addresses the wrong episode rather than reporting an error.
/// </summary>
public class ModelHelperEpisodeInputTests
{
    [Theory]
    [InlineData("1", 1)]
    [InlineData("0", 0)]
    [InlineData("26", 26)]
    [InlineData("-5", -5)]
    public void PlainNumbers_ParseWithNoEpisodeType(string input, int expected)
    {
        var (number, type, error) = ModelHelper.GetEpisodeNumberAndTypeFromInput(input);

        Assert.Equal(expected, number);
        Assert.Null(type);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("S3", 3, EpisodeType.Special)]
    [InlineData("C1", 1, EpisodeType.Credits)]
    [InlineData("T2", 2, EpisodeType.Trailer)]
    [InlineData("P4", 4, EpisodeType.Parody)]
    [InlineData("O5", 5, EpisodeType.Other)]
    [InlineData("E6", 6, EpisodeType.Episode)]
    public void TypePrefixes_MapToTheMatchingEpisodeType(string input, int expectedNumber, EpisodeType expectedType)
    {
        var (number, type, error) = ModelHelper.GetEpisodeNumberAndTypeFromInput(input);

        Assert.Equal(expectedNumber, number);
        Assert.Equal(expectedType, type);
        Assert.Null(error);
    }

    [Fact]
    public void TypePrefixes_AreCaseSensitive()
    {
        // Lower case is not accepted; it is reported rather than silently treated as a special.
        var (number, type, error) = ModelHelper.GetEpisodeNumberAndTypeFromInput("s3");

        Assert.Equal(0, number);
        Assert.Null(type);
        Assert.NotNull(error);
        Assert.Contains("Unknown episode type", error);
    }

    [Fact]
    public void AnUnrecognisedPrefix_IsReportedAsAnUnknownType()
    {
        var (number, type, error) = ModelHelper.GetEpisodeNumberAndTypeFromInput("X1");

        Assert.Equal(0, number);
        Assert.Null(type);
        Assert.Contains("Unknown episode type 'X'", error);
    }

    [Theory]
    [InlineData("SS")]
    [InlineData("Sabc")]
    [InlineData("S")]
    public void ANonNumericRemainder_IsReportedAsAParseFailure(string input)
    {
        var (number, type, error) = ModelHelper.GetEpisodeNumberAndTypeFromInput(input);

        Assert.Equal(0, number);
        Assert.Null(type);
        Assert.Contains("Unable to parse an int", error);
    }

    [Fact]
    public void TheParseFailureIsReportedBeforeTheUnknownTypeFailure()
    {
        // "XX" is both an unknown type and an unparseable number; the number check runs first.
        var (_, _, error) = ModelHelper.GetEpisodeNumberAndTypeFromInput("XX");

        Assert.Contains("Unable to parse an int", error);
    }
}
