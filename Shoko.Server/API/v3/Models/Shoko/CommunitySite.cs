
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Shoko.Server.API.v3.Models.Shoko;

/// <summary>
/// Currently in use community sites a user can have set.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum CommunitySite
{
    None = 0,
    AniDB = 1,
    Trakt = 2,
    Plex = 3,
}
