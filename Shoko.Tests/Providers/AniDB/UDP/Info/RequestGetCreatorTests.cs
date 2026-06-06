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

public class RequestGetCreatorTests
{
    private readonly RequestGetCreator _request;

    public RequestGetCreatorTests()
    {
        var loggerFactory = Mock.Of<ILoggerFactory>();
        var handler = Mock.Of<IUDPConnectionHandler>();
        _request = new RequestGetCreator(loggerFactory, handler);
    }

    [Fact]
    public void ParseResponse_ParsesAllFields()
    {
        // id|original_name|transcribed_name|type|pic|url_en|url_jp|wiki_en|wiki_jp|last_update
        var timestamp = 1234567890L;
        var expectedDate = DateTime.UnixEpoch.AddSeconds(timestamp).ToLocalTime();
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.CREATOR,
            Response = $"42|OriginalName|TranscribedName|1|pic.jpg|https://en.example|https://jp.example|https://en.wiki|https://jp.wiki|{timestamp}"
        };

        var result = _request.ParseResponse(response);

        Assert.Equal(UDPReturnCode.CREATOR, result.Code);
        Assert.Equal(42, result.Response.ID);
        Assert.Equal("TranscribedName", result.Response.Name);
        Assert.Equal("OriginalName", result.Response.OriginalName);
        Assert.Equal(CreatorType.Person, result.Response.Type);
        Assert.Equal("pic.jpg", result.Response.ImagePath);
        Assert.Equal("https://en.example", result.Response.EnglishHomepageUrl);
        Assert.Equal("https://jp.example", result.Response.JapaneseHomepageUrl);
        Assert.Equal("https://en.wiki", result.Response.EnglishWikiUrl);
        Assert.Equal("https://jp.wiki", result.Response.JapaneseWikiUrl);
        Assert.Equal(expectedDate, result.Response.LastUpdateAt);
    }

    [Fact]
    public void ParseResponse_ReplacesBackticksInNames()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.CREATOR,
            Response = "1|Name`With`Backticks|Transcribed`Name|1|||jp|||0"
        };

        var result = _request.ParseResponse(response);

        Assert.Equal("Name'With'Backticks", result.Response.OriginalName);
        Assert.Equal("Transcribed'Name", result.Response.Name);
    }

    [Fact]
    public void ParseResponse_NullifiesEmptyStrings()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.CREATOR,
            Response = "1|Name|Name|1|||jp|||0"
        };

        var result = _request.ParseResponse(response);

        Assert.Null(result.Response.ImagePath);
        Assert.Equal("jp", result.Response.JapaneseHomepageUrl);
        Assert.Null(result.Response.EnglishHomepageUrl);
        Assert.Null(result.Response.EnglishWikiUrl);
        Assert.Null(result.Response.JapaneseWikiUrl);
    }

    [Fact]
    public void ParseResponse_ParsesAllCreatorTypes()
    {
        // type 1 = Person, 2 = Company, 3 = Collaboration, 4 = Other
        static void TestType(int typeValue, CreatorType expected)
        {
            var r = new RequestGetCreator(Mock.Of<ILoggerFactory>(), Mock.Of<IUDPConnectionHandler>());
            var resp = new UDPResponse<string>
            {
                Code = UDPReturnCode.CREATOR,
                Response = $"1|Name|Name|{typeValue}||||||0"
            };
            Assert.Equal(expected, r.ParseResponse(resp).Response.Type);
        }

        TestType(1, CreatorType.Person);
        TestType(2, CreatorType.Company);
        TestType(3, CreatorType.Collaboration);
    }

    [Fact]
    public void ParseResponse_ThrowsOnLessThan10Parts()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.CREATOR,
            Response = "1|Name|Name|1|||||"
        };

        Assert.Throws<UnexpectedUDPResponseException>(() => _request.ParseResponse(response));
    }

    [Fact]
    public void ParseResponse_ThrowsOnNonIntCreatorID()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.CREATOR,
            Response = "abc|Name|Name|1||||||0"
        };

        Assert.Throws<UnexpectedUDPResponseException>(() => _request.ParseResponse(response));
    }

    [Fact]
    public void ParseResponse_ThrowsOnNonIntCreatorType()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.CREATOR,
            Response = "1|Name|Name|abc|||||0"
        };

        Assert.Throws<UnexpectedUDPResponseException>(() => _request.ParseResponse(response));
    }

    [Fact]
    public void ParseResponse_ThrowsOnNonIntLastUpdated()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.CREATOR,
            Response = "1|Name|Name|1|||||abc"
        };

        Assert.Throws<UnexpectedUDPResponseException>(() => _request.ParseResponse(response));
    }

    [Fact]
    public void ParseResponse_ReturnsNullOnNoSuchCreator()
    {
        var response = new UDPResponse<string>
        {
            Code = UDPReturnCode.NO_SUCH_CREATOR,
            Response = string.Empty
        };

        var result = _request.ParseResponse(response);

        Assert.Equal(UDPReturnCode.NO_SUCH_CREATOR, result.Code);
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
