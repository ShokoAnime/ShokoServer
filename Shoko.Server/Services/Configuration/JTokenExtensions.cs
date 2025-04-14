using Newtonsoft.Json.Linq;

namespace Shoko.Server.Services.Configuration;

internal static class JTokenExtensions
{
    internal static string ToJson(this JToken token)
        => token.Type is JTokenType.Boolean
            ? token.Value<bool>().ToString().ToLowerInvariant()
            : token.ToString();
}
