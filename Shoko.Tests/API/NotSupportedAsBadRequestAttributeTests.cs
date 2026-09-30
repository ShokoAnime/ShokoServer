using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Shoko.Server.API.Annotations;
using Xunit;

namespace Shoko.Tests.API;

/// <summary>
/// Covers how <see cref="NotSupportedAsBadRequestAttribute"/> answers what an
/// action threw.
/// </summary>
public class NotSupportedAsBadRequestAttributeTests
{
    private static ExceptionContext Context(Exception exception)
        => new(new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()), []) { Exception = exception };

    [Fact]
    public void ARefusalBecomesABadRequestWithTheReason()
    {
        var context = Context(new NotSupportedException("Nothing can link a movie from tmdb."));

        new NotSupportedAsBadRequestAttribute().OnException(context);

        Assert.True(context.ExceptionHandled);
        var result = Assert.IsType<BadRequestObjectResult>(context.Result);
        Assert.Equal("Nothing can link a movie from tmdb.", result.Value);
    }

    [Fact]
    public void AnyOtherExceptionIsLeftAlone()
    {
        var context = Context(new InvalidOperationException());

        new NotSupportedAsBadRequestAttribute().OnException(context);

        Assert.False(context.ExceptionHandled);
        Assert.Null(context.Result);
    }
}
