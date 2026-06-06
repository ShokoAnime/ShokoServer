using System;
using Microsoft.Extensions.Logging;
using Moq;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Generic;
using Shoko.Server.Providers.AniDB.UDP.Info;
using Xunit;

namespace Shoko.Tests.Providers.AniDB.UDP.Info;

public class RequestCalendarTests
{
    private readonly RequestCalendar _request;

    public RequestCalendarTests()
    {
        var loggerFactory = Mock.Of<ILoggerFactory>();
        var handler = Mock.Of<IUDPConnectionHandler>();
        _request = new RequestCalendar(loggerFactory, handler);
    }

    [Fact]
    public void ParseResponse_ParsesFutureEntries()
    {
        var futureDate = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds();
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.CALENDAR,
            Response = $"42|{futureDate}|0"
        };

        var result = _request.ParseResponse(response);

        Assert.Equal(UDPReturnCode.CALENDAR, result.Code);
        var entry = Assert.Single(result.Response.Next25Anime);
        Assert.Empty(result.Response.Previous25Anime);
        Assert.Equal(42, entry.AnimeID);
        Assert.NotNull(entry.ReleaseDate);
        Assert.Equal(ResponseCalendar.CalendarFlags.DateKnown, entry.DateFlags);
    }

    [Fact]
    public void ParseResponse_ParsesPastEntries()
    {
        var pastDate = DateTimeOffset.UtcNow.AddDays(-30).ToUnixTimeSeconds();
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.CALENDAR,
            Response = $"99|{pastDate}|0"
        };

        var result = _request.ParseResponse(response);

        Assert.Empty(result.Response.Next25Anime);
        var entry = Assert.Single(result.Response.Previous25Anime);
        Assert.Equal(99, entry.AnimeID);
    }

    [Fact]
    public void ParseResponse_ParsesMultipleEntries()
    {
        var futureDate = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds();
        var pastDate = DateTimeOffset.UtcNow.AddDays(-30).ToUnixTimeSeconds();
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.CALENDAR,
            Response = $"1|{futureDate}|0\n2|{pastDate}|0\n3|{futureDate}|0"
        };

        var result = _request.ParseResponse(response);

        Assert.Equal(2, result.Response.Next25Anime.Count);
        Assert.Single(result.Response.Previous25Anime);
    }

    [Fact]
    public void ParseResponse_SkipsInvalidParts()
    {
        var futureDate = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds();
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.CALENDAR,
            Response = $"1|{futureDate}|0\ninvalid_line\n2|{futureDate}|0"
        };

        var result = _request.ParseResponse(response);

        Assert.Equal(2, result.Response.Next25Anime.Count);
    }

    [Fact]
    public void ParseResponse_ParsesDateFlags()
    {
        var futureDate = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds();
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.CALENDAR,
            Response = $"1|{futureDate}|1"
        };

        var result = _request.ParseResponse(response);

        var entry = Assert.Single(result.Response.Next25Anime);
        Assert.Equal(ResponseCalendar.CalendarFlags.StartMonthDayUnknown, entry.DateFlags);
    }

    [Fact]
    public void ParseResponse_ReturnsNullOnEmpty()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.CALENDAR_EMPTY,
            Response = string.Empty
        };

        var result = _request.ParseResponse(response);

        Assert.Equal(UDPReturnCode.CALENDAR_EMPTY, result.Code);
        Assert.Null(result.Response);
    }
}
