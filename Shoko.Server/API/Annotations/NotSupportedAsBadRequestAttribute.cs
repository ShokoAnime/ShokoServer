using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Shoko.Server.API.Annotations;

/// <summary>
///   Answers 400 with the reason when an action throws a
///   <see cref="NotSupportedException"/>, such as the linking service refusing
///   a link no enabled provider takes, rather than letting it become a 500.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false)]
public sealed class NotSupportedAsBadRequestAttribute : ExceptionFilterAttribute
{
    /// <summary>
    ///   Turns a <see cref="NotSupportedException"/> into a 400 answer.
    /// </summary>
    /// <param name="context">What the action threw.</param>
    public override void OnException(ExceptionContext context)
    {
        if (context.Exception is not NotSupportedException exception)
            return;

        context.Result = new BadRequestObjectResult(exception.Message);
        context.ExceptionHandled = true;
    }
}
