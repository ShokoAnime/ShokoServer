using System;
using Microsoft.Extensions.Logging;
using Moq;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Generic;
using Shoko.Server.Providers.AniDB.UDP.Info;
using Xunit;

namespace Shoko.Tests.Providers.AniDB.UDP.Info;

public class RequestUpdatedAnimeTests
{
    private readonly RequestUpdatedAnime _request;

    public RequestUpdatedAnimeTests()
    {
        var loggerFactory = Mock.Of<ILoggerFactory>();
        var handler = Mock.Of<IUDPConnectionHandler>();
        _request = new RequestUpdatedAnime(loggerFactory, handler);
    }

    [Fact]
    public void ParseResponse_ParsesAllFields()
    {
        var timestamp = 1234567890L;
        var expectedDate = DateTime.UnixEpoch.AddSeconds(timestamp);
        // format: |count|date|id1,id2,id3
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.UPDATED,
            Response = $"|3|{timestamp}|101,102,103"
        };

        var result = _request.ParseResponse(response);

        Assert.Equal(UDPReturnCode.UPDATED, result.Code);
        Assert.Equal(3, result.Response.Count);
        Assert.Equal(expectedDate, result.Response.LastUpdated);
        Assert.Equal([101, 102, 103], result.Response.AnimeIDs);
    }

    [Fact]
    public void ParseResponse_ParsesSingleAnimeID()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.UPDATED,
            Response = "|1|1234567890|42"
        };

        var result = _request.ParseResponse(response);

        Assert.Equal(1, result.Response.Count);
        Assert.Equal([42], result.Response.AnimeIDs);
    }

    [Fact]
    public void ParseResponse_ReturnsNullOnNonUpdatedCode()
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
}
