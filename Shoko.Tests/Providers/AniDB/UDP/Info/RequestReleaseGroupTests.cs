using Microsoft.Extensions.Logging;
using Moq;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Providers.AniDB.UDP.Generic;
using Shoko.Server.Providers.AniDB.UDP.Info;
using Xunit;

namespace Shoko.Tests.Providers.AniDB.UDP.Info;

public class RequestReleaseGroupTests
{
    private readonly RequestReleaseGroup _request;

    public RequestReleaseGroupTests()
    {
        var loggerFactory = Mock.Of<ILoggerFactory>();
        var handler = Mock.Of<IUDPConnectionHandler>();
        _request = new RequestReleaseGroup(loggerFactory, handler);
    }

    [Fact]
    public void ParseResponse_ParsesAllFields()
    {
        // gid|rating|votes|acount|fcount|name|short|irc channel|irc server|url|picname
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.GROUP,
            Response = "42|750|100|15|200|MyGroup|GRP|#channel|irc.example.com|https://example.com|pic.jpg"
        };

        var result = _request.ParseResponse(response);

        Assert.Equal(UDPReturnCode.GROUP, result.Code);
        Assert.Equal(42, result.Response.ID);
        Assert.Equal(7.5m, result.Response.Rating);
        Assert.Equal(100, result.Response.Votes);
        Assert.Equal(15, result.Response.AnimeCount);
        Assert.Equal(200, result.Response.FileCount);
        Assert.Equal("MyGroup", result.Response.Name);
        Assert.Equal("GRP", result.Response.ShortName);
        Assert.Equal("#channel", result.Response.IrcChannel);
        Assert.Equal("irc.example.com", result.Response.IrcServer);
        Assert.Equal("https://example.com", result.Response.URL);
        Assert.Equal("pic.jpg", result.Response.Picture);
    }

    [Fact]
    public void ParseResponse_ParsesZeroRating()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.GROUP,
            Response = "1|0|0|0|0|||irc|irc.example.com||"
        };

        var result = _request.ParseResponse(response);

        Assert.Equal(0m, result.Response.Rating);
        Assert.Equal(0, result.Response.Votes);
        Assert.Equal(0, result.Response.AnimeCount);
        Assert.Equal(0, result.Response.FileCount);
    }

    [Fact]
    public void ParseResponse_ParsesEmptyStrings()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.GROUP,
            Response = "1|0|0|0|0|||irc|irc.example.com|url|"
        };

        var result = _request.ParseResponse(response);

        Assert.Empty(result.Response.Name);
        Assert.Empty(result.Response.ShortName);
        Assert.Equal("irc", result.Response.IrcChannel);
        Assert.Equal("irc.example.com", result.Response.IrcServer);
        Assert.Equal("url", result.Response.URL);
        Assert.Empty(result.Response.Picture);
    }

    [Fact]
    public void ParseResponse_ThrowsOnNonIntGroupID()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.GROUP,
            Response = "abc|0|0|0|0|Name|GRP|irc|irc.example.com|url|pic.jpg"
        };

        Assert.Throws<UnexpectedUDPResponseException>(() => _request.ParseResponse(response));
    }

    [Fact]
    public void ParseResponse_ThrowsOnNonIntRating()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.GROUP,
            Response = "1|abc|0|0|0|Name|GRP|irc|irc.example.com|url|pic.jpg"
        };

        Assert.Throws<UnexpectedUDPResponseException>(() => _request.ParseResponse(response));
    }

    [Fact]
    public void ParseResponse_ThrowsOnNonIntVotes()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.GROUP,
            Response = "1|0|abc|0|0|Name|GRP|irc|irc.example.com|url|pic.jpg"
        };

        Assert.Throws<UnexpectedUDPResponseException>(() => _request.ParseResponse(response));
    }

    [Fact]
    public void ParseResponse_ThrowsOnNonIntAnimeCount()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.GROUP,
            Response = "1|0|0|abc|0|Name|GRP|irc|irc.example.com|url|pic.jpg"
        };

        Assert.Throws<UnexpectedUDPResponseException>(() => _request.ParseResponse(response));
    }

    [Fact]
    public void ParseResponse_ThrowsOnNonIntFileCount()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.GROUP,
            Response = "1|0|0|0|abc|Name|GRP|irc|irc.example.com|url|pic.jpg"
        };

        Assert.Throws<UnexpectedUDPResponseException>(() => _request.ParseResponse(response));
    }

    [Fact]
    public void ParseResponse_ReturnsNullOnNoSuchGroup()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.NO_SUCH_GROUP,
            Response = string.Empty
        };

        var result = _request.ParseResponse(response);

        Assert.Equal(UDPReturnCode.NO_SUCH_GROUP, result.Code);
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
