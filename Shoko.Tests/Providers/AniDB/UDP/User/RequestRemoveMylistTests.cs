using Microsoft.Extensions.Logging;
using Moq;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Providers.AniDB.UDP.Generic;
using Shoko.Server.Providers.AniDB.UDP.User;
using Xunit;

namespace Shoko.Tests.Providers.AniDB.UDP.User;

public class RequestRemoveMylistTests
{
    private readonly RequestRemoveMylist _request;

    public RequestRemoveMylistTests()
    {
        var loggerFactory = Mock.Of<ILoggerFactory>();
        var handler = Mock.Of<IUDPConnectionHandler>();
        _request = new RequestRemoveMylist(loggerFactory, handler);
    }

    [Fact]
    public void ParseResponse_ReturnsVoid_OnEntryDeleted()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.MYLIST_ENTRY_DELETED,
            Response = string.Empty
        };

        var result = _request.ParseResponse(response);

        Assert.Equal(UDPReturnCode.MYLIST_ENTRY_DELETED, result.Code);
    }

    [Fact]
    public void ParseResponse_ReturnsVoid_OnNoSuchMylistEntry()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.NO_SUCH_MYLIST_ENTRY,
            Response = string.Empty
        };

        var result = _request.ParseResponse(response);

        Assert.Equal(UDPReturnCode.NO_SUCH_MYLIST_ENTRY, result.Code);
    }

    [Fact]
    public void ParseResponse_ReturnsVoid_OnNoSuchFile()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.NO_SUCH_FILE,
            Response = string.Empty
        };

        var result = _request.ParseResponse(response);

        Assert.Equal(UDPReturnCode.NO_SUCH_FILE, result.Code);
    }

    [Fact]
    public void ParseResponse_ReturnsVoid_OnNoSuchAnime()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.NO_SUCH_ANIME,
            Response = string.Empty
        };

        var result = _request.ParseResponse(response);

        Assert.Equal(UDPReturnCode.NO_SUCH_ANIME, result.Code);
    }

    [Fact]
    public void ParseResponse_ReturnsVoid_OnNoSuchEpisode()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.NO_SUCH_EPISODE,
            Response = string.Empty
        };

        var result = _request.ParseResponse(response);

        Assert.Equal(UDPReturnCode.NO_SUCH_EPISODE, result.Code);
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
