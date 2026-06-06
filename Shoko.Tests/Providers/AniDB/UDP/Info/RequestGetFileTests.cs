using System;
using Microsoft.Extensions.Logging;
using Moq;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;
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
        var handler = Mock.Of<IUDPConnectionHandler>();
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

    [Fact]
    public void ParseResponse_ParsesSingleEpisodeXref()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.FILE,
            Response = "1|2|9876|3||0|0|high|tv|jpn|eng|desc|0|file.mkv|G|GRP"
        };

        var result = _request.ParseResponse(response);
        var ep = Assert.Single(result.Response.EpisodeIDs);
        Assert.Equal(9876, ep.EpisodeID);
        Assert.Equal(100, ep.Percentage);
    }

    [Fact]
    public void ParseResponse_ParsesMultipleEpisodeXrefs_DefaultPercentage()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.FILE,
            Response = "1|2|9876'5432|3||0|0|high|tv|jpn|eng|desc|0|file.mkv|G|GRP"
        };

        var result = _request.ParseResponse(response);
        Assert.Equal(2, result.Response.EpisodeIDs.Count);
        Assert.Equal(9876, result.Response.EpisodeIDs[0].EpisodeID);
        Assert.Equal(50, result.Response.EpisodeIDs[0].Percentage);
        Assert.Equal(5432, result.Response.EpisodeIDs[1].EpisodeID);
        Assert.Equal(50, result.Response.EpisodeIDs[1].Percentage);
    }

    [Fact]
    public void ParseResponse_ParsesMultipleEpisodeXrefs_WithPercentages()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.FILE,
            Response = "1|2|9876,60'5432,40|3||0|0|high|tv|jpn|eng|desc|0|file.mkv|G|GRP"
        };

        var result = _request.ParseResponse(response);
        Assert.Equal(2, result.Response.EpisodeIDs.Count);
        Assert.Equal(9876, result.Response.EpisodeIDs[0].EpisodeID);
        Assert.Equal(60, result.Response.EpisodeIDs[0].Percentage);
        Assert.Equal(5432, result.Response.EpisodeIDs[1].EpisodeID);
        Assert.Equal(40, result.Response.EpisodeIDs[1].Percentage);
    }

    [Fact]
    public void ParseResponse_ParsesDeprecated()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.FILE,
            Response = "1|2|3|4||1|0|high|tv|jpn|eng|desc|0|file.mkv|G|GRP"
        };

        var result = _request.ParseResponse(response);
        Assert.True(result.Response.Deprecated);
    }

    [Fact]
    public void ParseResponse_ParsesVersionFlags()
    {
        // state = IsV2(4) | IsV3(8) | IsV4(16) | IsV5(32) = 60
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.FILE,
            Response = $"1|2|3|4||0|60|high|tv|jpn|eng|desc|0|file.mkv|G|GRP"
        };

        var result = _request.ParseResponse(response);
        Assert.Equal(5, result.Response.Version);
    }

    [Fact]
    public void ParseResponse_ParsesVersion2()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.FILE,
            Response = $"1|2|3|4||0|4|high|tv|jpn|eng|desc|0|file.mkv|G|GRP"
        };

        var result = _request.ParseResponse(response);
        Assert.Equal(2, result.Response.Version);
    }

    [Fact]
    public void ParseResponse_ParsesCensored()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.FILE,
            Response = $"1|2|3|4||0|128|high|tv|jpn|eng|desc|0|file.mkv|G|GRP"
        };

        var result = _request.ParseResponse(response);
        Assert.True(result.Response.Censored);
    }

    [Fact]
    public void ParseResponse_ParsesUncensored()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.FILE,
            Response = $"1|2|3|4||0|64|high|tv|jpn|eng|desc|0|file.mkv|G|GRP"
        };

        var result = _request.ParseResponse(response);
        Assert.False(result.Response.Censored);
    }

    [Fact]
    public void ParseResponse_ParsesCRCMatch()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.FILE,
            Response = $"1|2|3|4||0|1|high|tv|jpn|eng|desc|0|file.mkv|G|GRP"
        };

        var result = _request.ParseResponse(response);
        Assert.True(result.Response.CRCMatches);
    }

    [Fact]
    public void ParseResponse_ParsesCRCError()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.FILE,
            Response = $"1|2|3|4||0|2|high|tv|jpn|eng|desc|0|file.mkv|G|GRP"
        };

        var result = _request.ParseResponse(response);
        Assert.False(result.Response.CRCMatches);
    }

    [Fact]
    public void ParseResponse_ParsesChaptered()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.FILE,
            Response = $"1|2|3|4||0|4096|high|tv|jpn|eng|desc|0|file.mkv|G|GRP"
        };

        var result = _request.ParseResponse(response);
        Assert.True(result.Response.Chaptered);
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

    [Fact]
    public void ParseResponse_ParsesAudioLanguages()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.FILE,
            Response = "1|2|3|4||0|0|high|tv|jpn|eng|desc|0|file.mkv|G|GRP"
        };

        var result = _request.ParseResponse(response);
        Assert.Equal(["jpn"], result.Response.AudioLanguages);
    }

    [Fact]
    public void ParseResponse_ParsesMultipleAudioLanguages()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.FILE,
            Response = "1|2|3|4||0|0|high|tv|jpn'eng|eng|desc|0|file.mkv|G|GRP"
        };

        var result = _request.ParseResponse(response);
        Assert.Equal(["jpn", "eng"], result.Response.AudioLanguages);
    }

    [Fact]
    public void ParseResponse_ParsesSubtitleLanguages()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.FILE,
            Response = "1|2|3|4||0|0|high|tv|jpn|eng'fre|desc|0|file.mkv|G|GRP"
        };

        var result = _request.ParseResponse(response);
        Assert.Equal(["eng", "fre"], result.Response.SubtitleLanguages);
    }

    [Fact]
    public void ParseResponse_ExcludesNoneAudioLanguage()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.FILE,
            Response = "1|2|3|4||0|0|high|tv|none|eng|desc|0|file.mkv|G|GRP"
        };

        var result = _request.ParseResponse(response);
        Assert.Empty(result.Response.AudioLanguages);
    }

    [Fact]
    public void ParseResponse_ExcludesNoneSubtitleLanguage()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.FILE,
            Response = "1|2|3|4||0|0|high|tv|jpn|none|desc|0|file.mkv|G|GRP"
        };

        var result = _request.ParseResponse(response);
        Assert.Empty(result.Response.SubtitleLanguages);
    }

    [Fact]
    public void ParseResponse_ParsesOtherEpisodes_Format1()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.FILE,
            Response = "1|2|3|4|1111'50'2222'75|0|0|high|tv|jpn|eng|desc|0|file.mkv|G|GRP"
        };

        var result = _request.ParseResponse(response);
        Assert.Equal(2, result.Response.OtherEpisodes.Count);
        Assert.Equal(1111, result.Response.OtherEpisodes[0].EpisodeID);
        Assert.Equal(50, result.Response.OtherEpisodes[0].Percentage);
        Assert.Equal(2222, result.Response.OtherEpisodes[1].EpisodeID);
        Assert.Equal(75, result.Response.OtherEpisodes[1].Percentage);
    }

    [Fact]
    public void ParseResponse_ParsesOtherEpisodes_Format2()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.FILE,
            Response = "1|2|3|4|1111,50'2222,75|0|0|high|tv|jpn|eng|desc|0|file.mkv|G|GRP"
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
