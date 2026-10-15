using Microsoft.Extensions.Logging;
using Moq;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.UDP;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Providers.AniDB.UDP.Generic;
using Shoko.Server.Providers.AniDB.UDP.User;
using Xunit;

namespace Shoko.Tests.Providers.AniDB.UDP.User;

public class RequestUpdateMylistTests
{
    private readonly RequestUpdateMylist _request;

    public RequestUpdateMylistTests()
    {
        var loggerFactory = Mock.Of<ILoggerFactory>();
        var handler = Mock.Of<IAniDbUdpRequestChannel>();
        _request = new RequestUpdateMylist(loggerFactory, handler);
    }

    [Theory]
    [InlineData(UDPReturnCode.MYLIST_ENTRY_EDITED)]
    [InlineData(UDPReturnCode.NO_SUCH_MYLIST_ENTRY)]
    [InlineData(UDPReturnCode.NO_SUCH_FILE)]
    [InlineData(UDPReturnCode.NO_SUCH_ANIME)]
    [InlineData(UDPReturnCode.NO_SUCH_GROUP)]
    public void ParseResponse_ReturnsVoid_OnAcceptedReturnCode(UDPReturnCode code)
    {
        var response = new UDPResponse<string>
        {
            Code = code,
            Response = string.Empty
        };

        var result = _request.ParseResponse(response);

        Assert.Equal(code, result.Code);
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
