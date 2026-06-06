using Microsoft.Extensions.Logging;
using Moq;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Providers.AniDB.UDP.Generic;
using Shoko.Server.Providers.AniDB.UDP.Info;
using Xunit;

namespace Shoko.Tests.Providers.AniDB.UDP.Info;

public class RequestGetEpisodeTests
{
    private readonly RequestGetEpisode _request;

    public RequestGetEpisodeTests()
    {
        var loggerFactory = Mock.Of<ILoggerFactory>();
        var handler = Mock.Of<IUDPConnectionHandler>();
        _request = new RequestGetEpisode(loggerFactory, handler);
    }

    [Fact]
    public void ParseResponse_ParsesEpisodeIDAndAnimeID()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.EPISODE,
            Response = "98765|43210|1200|750|50|1|Title|Romaji|Kanji|1234567890|1"
        };

        var result = _request.ParseResponse(response);

        Assert.Equal(UDPReturnCode.EPISODE, result.Code);
        Assert.Equal(98765, result.Response.EpisodeID);
        Assert.Equal(43210, result.Response.AnimeID);
    }

    [Fact]
    public void ParseResponse_ThrowsOnLessThanTwoParts()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.EPISODE,
            Response = "98765"
        };

        Assert.Throws<UnexpectedUDPResponseException>(() => _request.ParseResponse(response));
    }

    [Fact]
    public void ParseResponse_ThrowsOnNonIntEpisodeID()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.EPISODE,
            Response = "abc|43210|1200|750|50|1|Title|Romaji|Kanji|1234567890|1"
        };

        Assert.Throws<UnexpectedUDPResponseException>(() => _request.ParseResponse(response));
    }

    [Fact]
    public void ParseResponse_ThrowsOnNonIntAnimeID()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.EPISODE,
            Response = "98765|abc|1200|750|50|1|Title|Romaji|Kanji|1234567890|1"
        };

        Assert.Throws<UnexpectedUDPResponseException>(() => _request.ParseResponse(response));
    }

    [Fact]
    public void ParseResponse_ReturnsNullOnNoSuchEpisode()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.NO_SUCH_EPISODE,
            Response = string.Empty
        };

        var result = _request.ParseResponse(response);

        Assert.Equal(UDPReturnCode.NO_SUCH_EPISODE, result.Code);
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
