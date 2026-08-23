using System;
using Microsoft.Extensions.Logging;
using Moq;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Providers.AniDB.UDP.Generic;
using Shoko.Server.Providers.AniDB.UDP.User;
using Xunit;

namespace Shoko.Tests.Providers.AniDB.UDP.User;

public class RequestAddMyListTests
{
    private readonly RequestAddMyList _request;

    public RequestAddMyListTests()
    {
        var loggerFactory = Mock.Of<ILoggerFactory>();
        var handler = Mock.Of<IUDPConnectionHandler>();
        _request = new RequestAddMyList(loggerFactory, handler);
    }

    [Fact]
    public void ParseResponse_ParsesMyListID_OnEntryAdded()
    {
        _request.FileID = 678;
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.MYLIST_ENTRY_ADDED,
            Response = "12345"
        };

        var result = _request.ParseResponse(response);

        Assert.Equal(UDPReturnCode.MYLIST_ENTRY_ADDED, result.Code);
        var entry = result.Response;
        Assert.NotNull(entry);
        Assert.Equal(12345ul, entry.MyListID);
        Assert.Equal(678, entry.FileID);
        Assert.Equal(MyListFileState.Normal, entry.FileState);
        Assert.Equal(MyListState.Unknown, entry.State);
        Assert.False(entry.IsViewed);
        Assert.Equal(DateOnly.FromDateTime(DateTime.Today), entry.UpdatedAt);
    }

    [Fact]
    public void ParseResponse_ParsesMyListID_OnEntryAddedByEd2k()
    {
        _request.ED2K = "abc123";
        _request.Size = 4096;
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.MYLIST_ENTRY_ADDED,
            Response = "12345"
        };

        var entry = _request.ParseResponse(response).Response;

        Assert.NotNull(entry);
        Assert.Equal(12345ul, entry.MyListID);
        Assert.Equal("abc123", entry.ED2K);
        Assert.Equal(4096, entry.Size);
    }

    [Fact]
    public void ParseResponse_IgnoresEntryCount_OnGenericEntryAdded()
    {
        // a generic add returns the number of entries added, not a list ID
        _request.AnimeID = 42;
        _request.EpisodeType = EpisodeType.Episode;
        _request.EpisodeNumber = 1;
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.MYLIST_ENTRY_ADDED,
            Response = "1"
        };

        var entry = _request.ParseResponse(response).Response;

        Assert.NotNull(entry);
        Assert.Equal(0ul, entry.MyListID);
        Assert.Equal(42, entry.AnimeID);
    }

    [Fact]
    public void ParseResponse_EntryAdded_ReflectsRequestedState()
    {
        _request.FileID = 678;
        _request.State = MyListState.HDD;
        _request.FileState = MyListFileState.SelfEdited;
        _request.IsViewed = true;
        _request.ViewedAt = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        _request.Storage = "HDD";
        _request.Source = "TV";
        _request.Other = "notes";

        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.MYLIST_ENTRY_ADDED,
            Response = "12345"
        };

        var result = _request.ParseResponse(response);

        var entry = result.Response;
        Assert.Equal(MyListState.HDD, entry.State);
        Assert.Equal(MyListFileState.SelfEdited, entry.FileState);
        Assert.True(entry.IsViewed);
        Assert.Equal(_request.ViewedAt, entry.ViewedAt);
        Assert.Equal("HDD", entry.Storage);
        Assert.Equal("TV", entry.Source);
        Assert.Equal("notes", entry.Other);
    }

    [Fact]
    public void ParseResponse_ParsesExistingEntry_OnFileAlreadyInMyList()
    {
        // lid|fid|eid|aid|gid|date|state|viewdate|storage|source|other|filestate
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.FILE_ALREADY_IN_MYLIST,
            Response = "0|678|9876|42|0|1700000000|1|1700000100|HDD|TV|notes|0"
        };

        var result = _request.ParseResponse(response);

        Assert.Equal(UDPReturnCode.FILE_ALREADY_IN_MYLIST, result.Code);
        var entry = result.Response;
        Assert.NotNull(entry);
        Assert.Equal(0ul, entry.MyListID);
        Assert.Equal(678, entry.FileID);
        Assert.Equal(9876, entry.EpisodeID);
        Assert.Equal(42, entry.AnimeID);
        Assert.Equal(MyListState.HDD, entry.State);
        Assert.Equal(MyListFileState.Normal, entry.FileState);
        Assert.True(entry.IsViewed);
        Assert.NotNull(entry.ViewedAt);
        Assert.Equal(new DateOnly(2023, 11, 14), entry.UpdatedAt);
        Assert.Equal("HDD", entry.Storage);
        Assert.Equal("TV", entry.Source);
        Assert.Equal("notes", entry.Other);
    }

    [Fact]
    public void ParseResponse_ExistingGenericEntry_ParsesFileState()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.FILE_ALREADY_IN_MYLIST,
            Response = "555|0|9876|42|0|1700000000|1|0|||-|100"
        };

        var entry = _request.ParseResponse(response).Response;

        Assert.NotNull(entry);
        Assert.Equal(555ul, entry.MyListID);
        Assert.Equal(MyListFileState.Other, entry.FileState);
        Assert.Equal(9876, entry.EpisodeID);
    }

    [Fact]
    public void ParseResponse_ExistingEntry_Unwatched()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.FILE_ALREADY_IN_MYLIST,
            Response = "0|678|9876|42|0|1700000000|1|0|HDD|TV|notes|0"
        };

        var result = _request.ParseResponse(response);

        Assert.False(result.Response.IsViewed);
        Assert.Null(result.Response.ViewedAt);
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
