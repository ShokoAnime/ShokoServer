using Microsoft.Extensions.Logging;
using Moq;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Providers.AniDB.UDP.Generic;
using Shoko.Server.Providers.AniDB.UDP.Info;
using Xunit;

namespace Shoko.Tests.Providers.AniDB.UDP.Info;

public class RequestReleaseGroupStatusTests
{
    private readonly RequestReleaseGroupStatus _request;

    public RequestReleaseGroupStatusTests()
    {
        var loggerFactory = Mock.Of<ILoggerFactory>();
        var handler = Mock.Of<IUDPConnectionHandler>();
        _request = new RequestReleaseGroupStatus(loggerFactory, handler)
        {
            AnimeID = 1234
        };
    }

    [Fact]
    public void ParseResponse_ParsesSingleGroupEntry()
    {
        // gid|name|completion_state|last_episode|rating|votes|episode_range
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.GROUP_STATUS,
            Response = "42|MyGroup|3|12|750|100|1-12"
        };

        var result = _request.ParseResponse(response);

        Assert.Equal(UDPReturnCode.GROUP_STATUS, result.Code);
        var group = Assert.Single(result.Response);
        Assert.Equal(1234, group.AnimeID);
        Assert.Equal(42, group.GroupID);
        Assert.Equal("MyGroup", group.GroupName);
        Assert.Equal(Group_CompletionStatus.Complete, group.CompletionState);
        Assert.Equal(12, group.LastEpisodeNumber);
        Assert.Equal(7.5m, group.Rating);
        Assert.Equal(100, group.Votes);
        Assert.Equal(["1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12"], group.ReleasedEpisodes);
    }

    [Fact]
    public void ParseResponse_ParsesMultipleGroups()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.GROUP_STATUS,
            Response = "1|GroupA|3|12|750|100|1-12\n2|GroupB|1|5|500|50|1-5"
        };

        var result = _request.ParseResponse(response);

        Assert.Equal(2, result.Response.Count);
        Assert.Equal("GroupA", result.Response[0].GroupName);
        Assert.Equal("GroupB", result.Response[1].GroupName);
    }

    [Fact]
    public void ParseResponse_SkipsLinesWithWrongFieldCount()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.GROUP_STATUS,
            Response = "1|GroupA|3|12|750|100|1-12\ninvalid_line_without_pipes\n2|GroupB|1|5|500|50|1-5"
        };

        var result = _request.ParseResponse(response);

        Assert.Equal(2, result.Response.Count);
    }

    [Fact]
    public void ParseResponse_ParsesEpisodeRangeWithDashes()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.GROUP_STATUS,
            Response = "1|Group|3|9|750|100|1-9"
        };

        var result = _request.ParseResponse(response);

        Assert.Equal(["1", "2", "3", "4", "5", "6", "7", "8", "9"], result.Response[0].ReleasedEpisodes);
    }

    [Fact]
    public void ParseResponse_ParsesCommaSeparatedEpisodes()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.GROUP_STATUS,
            Response = "1|Group|3|7|750|100|5,7"
        };

        var result = _request.ParseResponse(response);

        Assert.Equal(["5", "7"], result.Response[0].ReleasedEpisodes);
    }

    [Fact]
    public void ParseResponse_ParsesMixedFormat()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.GROUP_STATUS,
            Response = "1|Group|3|8|750|100|5,7"
        };

        var result = _request.ParseResponse(response);

        Assert.Equal(["5", "7"], result.Response[0].ReleasedEpisodes);
    }

    [Fact]
    public void ParseResponse_ParsesDroppedCompletionState()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.GROUP_STATUS,
            Response = "1|Group|4|5|0|0|1-5"
        };

        var result = _request.ParseResponse(response);

        Assert.Equal(Group_CompletionStatus.Dropped, result.Response[0].CompletionState);
    }

    [Fact]
    public void ParseResponse_ParsesOngoingCompletionState()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.GROUP_STATUS,
            Response = "1|Group|1|5|0|0|1-5"
        };

        var result = _request.ParseResponse(response);

        Assert.Equal(Group_CompletionStatus.Ongoing, result.Response[0].CompletionState);
    }

    [Fact]
    public void ParseResponse_ReturnsNullOnNoSuchAnime()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.NO_SUCH_ANIME,
            Response = string.Empty
        };

        var result = _request.ParseResponse(response);

        Assert.Equal(UDPReturnCode.NO_SUCH_ANIME, result.Code);
        Assert.Null(result.Response);
    }

    [Fact]
    public void ParseResponse_ReturnsNullOnNoGroupsFound()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.NO_GROUPS_FOUND,
            Response = string.Empty
        };

        var result = _request.ParseResponse(response);

        Assert.Equal(UDPReturnCode.NO_GROUPS_FOUND, result.Code);
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

    [Fact]
    public void ParseResponse_EmptyResponseReturnsEmptyList()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.GROUP_STATUS,
            Response = ""
        };

        var result = _request.ParseResponse(response);

        Assert.Empty(result.Response);
    }
}
