using System;
using Microsoft.Extensions.Logging;
using Moq;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.UDP;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Providers.AniDB.UDP.Generic;
using Shoko.Server.Providers.AniDB.UDP.Info;
using Xunit;

namespace Shoko.Tests.Providers.AniDB.UDP.Info;

public class RequestGetFileTests
{
    private readonly RequestGetFile _request;

    public RequestGetFileTests()
    {
        var loggerFactory = Mock.Of<ILoggerFactory>();
        var handler = Mock.Of<IAniDbUdpRequestChannel>();
        _request = new RequestGetFile(loggerFactory, handler);
    }

    [Fact]
    public void ParseResponse_ParsesAllBasicFields()
    {
        // fileid|anime|episode|group|other eps|deprecated|state|quality|source|audio lang|sub lang|file desc|air date|filename|group name|group name short
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.FILE,
            Response = "12345|678|9876|42||0|0|high|tv|jpn|eng|Some description|1234567890|episode.mkv|MyGroup|GRP"
        };

        var result = _request.ParseResponse(response);

        Assert.Equal(UDPReturnCode.FILE, result.Code);
        var data = result.Response;
        Assert.Equal(12345, data.FileID);
        Assert.Equal(678, data.AnimeID);
        Assert.Equal(42, data.GroupID);
        Assert.Equal("MyGroup", data.GroupName);
        Assert.Equal("GRP", data.GroupShortName);
        Assert.False(data.Deprecated);
        Assert.Equal(1, data.Version);
        Assert.Null(data.Censored);
        Assert.Null(data.CRCMatches);
        Assert.False(data.Chaptered);
        Assert.Equal(GetFile_Quality.High, data.Quality);
        Assert.Equal(GetFile_Source.TV, data.Source);
        Assert.Equal("Some description", data.Description);
        Assert.Equal("episode.mkv", data.Filename);
        Assert.Equal(new DateOnly(2009, 2, 13), data.ReleasedAt);
    }

    [Theory]
    [InlineData("9876", new[] { 9876 }, new[] { 100 })] // A single episode is the whole file.
    [InlineData("9876'5432", new[] { 9876, 5432 }, new[] { 50, 50 })] // Without percentages the episodes split the file evenly.
    [InlineData("9876,60'5432,40", new[] { 9876, 5432 }, new[] { 60, 40 })]
    public void ParseResponse_ParsesEpisodeXrefs(string episodeField, int[] expectedEpisodeIDs, int[] expectedPercentages)
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.FILE,
            Response = $"1|2|{episodeField}|3||0|0|high|tv|jpn|eng|desc|0|file.mkv|G|GRP"
        };

        var result = _request.ParseResponse(response);

        var xrefs = result.Response.EpisodeIDs;
        Assert.Equal(expectedEpisodeIDs, xrefs.ConvertAll(x => x.EpisodeID));
        Assert.Equal(expectedPercentages, xrefs.ConvertAll(x => (int)x.Percentage));
    }

    [Theory]
    [InlineData("1", "high", true, GetFile_Quality.High)]
    [InlineData("0", "corrupted", false, GetFile_Quality.Corrupted)]
    [InlineData("1", "corrupted", true, GetFile_Quality.Corrupted)]
    public void ParseResponse_KeepsDeprecationAndQualityIndependent(string deprecated, string quality, bool expectedDeprecated, GetFile_Quality expectedQuality)
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.FILE,
            Response = $"1|2|3|4||{deprecated}|0|{quality}|tv|jpn|eng|desc|0|file.mkv|G|GRP"
        };

        var result = _request.ParseResponse(response);

        Assert.Equal(expectedDeprecated, result.Response.Deprecated);
        Assert.Equal(expectedQuality, result.Response.Quality);
    }

    [Theory]
    [InlineData(60, 5, null, null, false)] // IsV2 | IsV3 | IsV4 | IsV5: the highest version set wins.
    [InlineData(4, 2, null, null, false)] // IsV2.
    [InlineData(128, 1, true, null, false)] // Censored.
    [InlineData(64, 1, false, null, false)] // Uncensored.
    [InlineData(1, 1, null, true, false)] // CRCMatch.
    [InlineData(2, 1, null, false, false)] // CRCErr.
    [InlineData(4096, 1, null, null, true)] // Chaptered.
    public void ParseResponse_ParsesTheStateFlags(int state, int expectedVersion, bool? expectedCensored, bool? expectedCrcMatches, bool expectedChaptered)
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.FILE,
            Response = $"1|2|3|4||0|{state}|high|tv|jpn|eng|desc|0|file.mkv|G|GRP"
        };

        var result = _request.ParseResponse(response);

        Assert.Equal(expectedVersion, result.Response.Version);
        Assert.Equal(expectedCensored, result.Response.Censored);
        Assert.Equal(expectedCrcMatches, result.Response.CRCMatches);
        Assert.Equal(expectedChaptered, result.Response.Chaptered);
    }

    [Theory]
    [InlineData("veryhigh", GetFile_Quality.VeryHigh)]
    [InlineData("high", GetFile_Quality.High)]
    [InlineData("med", GetFile_Quality.Medium)]
    [InlineData("medium", GetFile_Quality.Medium)]
    [InlineData("low", GetFile_Quality.Low)]
    [InlineData("verylow", GetFile_Quality.VeryLow)]
    [InlineData("corrupted", GetFile_Quality.Corrupted)]
    [InlineData("eyecancer", GetFile_Quality.EyeCancer)]
    [InlineData("unknown", GetFile_Quality.Unknown)]
    public void ParseResponse_ParsesAllQualityValues(string quality, GetFile_Quality expected)
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.FILE,
            Response = $"1|2|3|4||0|0|{quality}|tv|jpn|eng|desc|0|file.mkv|G|GRP"
        };

        var result = _request.ParseResponse(response);
        Assert.Equal(expected, result.Response.Quality);
    }

    [Theory]
    [InlineData("tv", GetFile_Source.TV)]
    [InlineData("www", GetFile_Source.Web)]
    [InlineData("dvd", GetFile_Source.DVD)]
    [InlineData("bluray", GetFile_Source.BluRay)]
    [InlineData("vhs", GetFile_Source.VHS)]
    [InlineData("hkdvd", GetFile_Source.HKDVD)]
    [InlineData("hddvd", GetFile_Source.HDDVD)]
    [InlineData("hdtv", GetFile_Source.HDTV)]
    [InlineData("dtv", GetFile_Source.DTV)]
    [InlineData("camcorder", GetFile_Source.Camcorder)]
    [InlineData("vcd", GetFile_Source.VCD)]
    [InlineData("svcd", GetFile_Source.SVCD)]
    [InlineData("ld", GetFile_Source.LaserDisc)]
    [InlineData("8mm", GetFile_Source.Film8mm)]
    [InlineData("16mm", GetFile_Source.Film16mm)]
    [InlineData("35mm", GetFile_Source.Film35mm)]
    public void ParseResponse_ParsesAllSourceValues(string source, GetFile_Source expected)
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.FILE,
            Response = $"1|2|3|4||0|0|high|{source}|jpn|eng|desc|0|file.mkv|G|GRP"
        };

        var result = _request.ParseResponse(response);
        Assert.Equal(expected, result.Response.Source);
    }

    [Theory]
    [InlineData("jpn", new[] { "jpn" })]
    [InlineData("jpn'eng", new[] { "jpn", "eng" })]
    [InlineData("none", new string[0])] // AniDB's "none" is not a language.
    public void ParseResponse_ParsesAudioLanguages(string audioField, string[] expected)
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.FILE,
            Response = $"1|2|3|4||0|0|high|tv|{audioField}|eng|desc|0|file.mkv|G|GRP"
        };

        var result = _request.ParseResponse(response);

        Assert.Equal(expected, result.Response.AudioLanguages);
    }

    [Theory]
    [InlineData("eng'fre", new[] { "eng", "fre" })]
    [InlineData("none", new string[0])] // AniDB's "none" is not a language.
    public void ParseResponse_ParsesSubtitleLanguages(string subtitleField, string[] expected)
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.FILE,
            Response = $"1|2|3|4||0|0|high|tv|jpn|{subtitleField}|desc|0|file.mkv|G|GRP"
        };

        var result = _request.ParseResponse(response);

        Assert.Equal(expected, result.Response.SubtitleLanguages);
    }

    [Theory]
    [InlineData("1111'50'2222'75")] // Format 1: number'percent'number'percent.
    [InlineData("1111,50'2222,75")] // Format 2: number,percent'number,percent.
    public void ParseResponse_ParsesOtherEpisodes(string otherEpisodesField)
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.FILE,
            Response = $"1|2|3|4|{otherEpisodesField}|0|0|high|tv|jpn|eng|desc|0|file.mkv|G|GRP"
        };

        var result = _request.ParseResponse(response);

        Assert.Equal(2, result.Response.OtherEpisodes.Count);
        Assert.Equal(1111, result.Response.OtherEpisodes[0].EpisodeID);
        Assert.Equal(50, result.Response.OtherEpisodes[0].Percentage);
        Assert.Equal(2222, result.Response.OtherEpisodes[1].EpisodeID);
        Assert.Equal(75, result.Response.OtherEpisodes[1].Percentage);
    }

    [Fact]
    public void ParseResponse_NoGroup_GroupIDIsNull()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.FILE,
            Response = "1|2|3|0||0|0|high|tv|jpn|eng|desc|0|file.mkv|G|GRP"
        };

        var result = _request.ParseResponse(response);
        Assert.Null(result.Response.GroupID);
    }

    [Fact]
    public void ParseResponse_NullAirDate_WhenZero()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.FILE,
            Response = "1|2|3|4||0|0|high|tv|jpn|eng|desc|0|file.mkv|G|GRP"
        };

        var result = _request.ParseResponse(response);
        Assert.Null(result.Response.ReleasedAt);
    }

    [Fact]
    public void ParseResponse_ThrowsOnWrongPartsCount()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.FILE,
            Response = "1|2|3|4||0|0|high"
        };

        Assert.Throws<UnexpectedUDPResponseException>(() => _request.ParseResponse(response));
    }

    [Fact]
    public void ParseResponse_ReturnsNullOnNoSuchFile()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.NO_SUCH_FILE,
            Response = string.Empty
        };

        var result = _request.ParseResponse(response);
        Assert.Equal(UDPReturnCode.NO_SUCH_FILE, result.Code);
        Assert.Null(result.Response);
    }

    [Fact]
    public void ParseResponse_ThrowsOnUnhandledCode()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.BANNED,
            Response = string.Empty
        };

        Assert.Throws<UnexpectedUDPResponseException>(() => _request.ParseResponse(response));
    }
}
