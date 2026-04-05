using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Shoko.Server.Services.Configuration;

internal static class JTokenExtensions
{
    internal static string ToJson(this JToken token)
        => token.Type switch
        {
            JTokenType.Boolean => token.Value<bool>().ToString().ToLowerInvariant(),
            JTokenType.String => JsonConvert.SerializeObject(token.Value<string>()),
            _ => token.ToString(),
        };
}
