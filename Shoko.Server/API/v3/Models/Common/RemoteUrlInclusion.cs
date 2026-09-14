using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace Shoko.Server.API.v3.Models.Common;

/// <summary>
/// Which images to hand out a source URL for.
/// </summary>
/// <remarks>
/// Deliberately not <see cref="IncludeOnlyFilter"/>. That one narrows the
/// result set, so <c>only</c> there means "return just the matching
/// elements"; here nothing is filtered and the choice is only whether to fill
/// in a field, so reusing it would invite reading <c>only</c> as "give me just
/// the unavailable images".
/// </remarks>
[JsonConverter(typeof(StringEnumConverter), typeof(CamelCaseNamingStrategy))]
public enum RemoteUrlInclusion
{
    /// <summary>
    /// Never hand out a source URL.
    /// </summary>
    False = 0,

    /// <summary>
    /// Hand out a source URL for every image, whether or not the server holds
    /// it locally.
    /// </summary>
    True = 1,

    /// <summary>
    /// Hand out a source URL only for images the server does not hold locally,
    /// so its presence is itself the signal that the image cannot be served
    /// from here.
    /// </summary>
    WhenUnavailable = 2,
}
