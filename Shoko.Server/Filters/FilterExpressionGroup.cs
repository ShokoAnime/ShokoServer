using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
namespace Shoko.Server.Filters;

[JsonConverter(typeof(StringEnumConverter))]
public enum FilterExpressionGroup
{
    Info,
    Logic,
    Function,
    Selector
}
