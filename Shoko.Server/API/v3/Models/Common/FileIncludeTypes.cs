using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Shoko.Server.API.v3.Models.Common;

[JsonConverter(typeof(StringEnumConverter))]
public enum FileExcludeTypes
{
    Watched,
    Variations,
    Duplicates,
    Unrecognized,
    ManualLinks,
}

[JsonConverter(typeof(StringEnumConverter))]
public enum FileNonDefaultIncludeType
{
    Ignored,
    MediaInfo,
    XRefs,
    AbsolutePaths
}

[JsonConverter(typeof(StringEnumConverter))]
public enum FileIncludeOnlyType
{
    Watched,
    Variations,
    Duplicates,
    Unrecognized,
    ManualLinks,
    Ignored
}
