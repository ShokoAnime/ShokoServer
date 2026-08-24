using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Shoko.Server.API.v3.Models.Action;

/// <summary>
///   Turns an invocation endpoint's optional JSON body into the parameter
///   dictionary the action service takes.
/// </summary>
/// <remarks>
///   The five invoke endpoints each resolve a different scope entity, so they
///   cannot share a single action method, but the body handling is identical
///   and lives here rather than five times over.
/// </remarks>
public static class ActionParameterBody
{
    /// <summary>
    ///   Converts a request body into invocation parameters.
    /// </summary>
    /// <remarks>
    ///   Values stay as <see cref="JToken"/>s. The service serialises the
    ///   dictionary straight back to JSON before populating the action, so
    ///   keeping the parsed tokens avoids a lossy trip through CLR primitives.
    /// </remarks>
    /// <param name="body">
    ///   The request body, or <see langword="null"/> when the caller sent none.
    /// </param>
    /// <returns>
    ///   The parameters, or <see langword="null"/> when there was no body —
    ///   which is how every action was invoked before parameters existed, and
    ///   how a parameterless one still is.
    /// </returns>
    public static IReadOnlyDictionary<string, object?>? ToParameters(this JObject? body)
        => body is null ? null : body.Properties().ToDictionary(x => x.Name, x => (object?)x.Value);
}
