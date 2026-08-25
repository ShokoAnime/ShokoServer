using System;
using Microsoft.Extensions.Logging;
using Moq;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Providers.AniDB.UDP.Generic;
using Shoko.Server.Providers.AniDB.UDP.User;
using Xunit;

namespace Shoko.Tests.Providers.AniDB.UDP.User;

public class RequestGetMylistTests
{
    private readonly RequestGetMylist _request;

    public RequestGetMylistTests()
    {
        var loggerFactory = Mock.Of<ILoggerFactory>();
        var handler = Mock.Of<IUDPConnectionHandler>();
        _request = new RequestGetMylist(loggerFactory, handler);
    }

    [Fact]
    public void ParseResponse_ParsesAllBasicFields()
    {
        // lid|fid|eid|aid|gid|date|state|viewdate|storage|source|other|filestate
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.MYLIST,
            Response = "12345|678|9876|42|0|1700000000|1|1700000100|HDD|TV|notes|0"
        };

        var result = _request.ParseResponse(response);

        Assert.Equal(UDPReturnCode.MYLIST, result.Code);
        var entry = result.Response;
        Assert.NotNull(entry);
        Assert.Equal(12345ul, entry.MylistID);
        Assert.Equal(678, entry.FileID);
        Assert.Equal(9876, entry.EpisodeID);
        Assert.Equal(42, entry.AnimeID);
        Assert.Equal(MylistState.HDD, entry.State);
        Assert.Equal(MylistFileState.Normal, entry.FileState);
        Assert.True(entry.IsViewed);
        Assert.NotNull(entry.ViewedAt);
        Assert.Equal(new DateOnly(2023, 11, 14), entry.UpdatedAt);
        Assert.Equal("HDD", entry.Storage);
        Assert.Equal("TV", entry.Source);
        Assert.Equal("notes", entry.Other);
    }

    [Fact]
    public void ParseResponse_ParsesGenericFileState()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.MYLIST,
            Response = "12345|0|9876|42|0|1700000000|1|0|HDD|TV|notes|100"
        };

        var result = _request.ParseResponse(response);

        Assert.Equal(MylistFileState.Other, result.Response.FileState);
        Assert.Equal(0, result.Response.FileID);
    }

    [Fact]
    public void ParseResponse_DefaultsFileState_WhenMissing()
    {
        // no filestate field
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.MYLIST,
            Response = "12345|678|9876|42|0|1700000000|1|0|HDD|TV|notes"
        };

        var result = _request.ParseResponse(response);

        Assert.Equal(MylistFileState.Normal, result.Response.FileState);
    }

    [Fact]
    public void ParseResponse_Unwatched_WhenViewDateIsZero()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.MYLIST,
            Response = "12345|678|9876|42|0|1700000000|1|0|HDD|TV|notes|0"
        };

        var result = _request.ParseResponse(response);

        Assert.False(result.Response.IsViewed);
        Assert.Null(result.Response.ViewedAt);
    }

    [Fact]
    public void ParseResponse_ReturnsNullOnNoSuchEntry()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.NO_SUCH_ENTRY,
            Response = string.Empty
        };

        var result = _request.ParseResponse(response);

        Assert.Equal(UDPReturnCode.NO_SUCH_ENTRY, result.Code);
        Assert.Null(result.Response);
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
