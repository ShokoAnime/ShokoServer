using System.Runtime.Serialization;

namespace Shoko.Abstractions.Connectivity.Enums;

/// <summary>
/// The type of HTTP request to use for a connectivity check.
/// </summary>
public enum ConnectivityCheckType
{
    /// <summary>
    /// Use an HTTP GET request.
    /// </summary>
    [EnumMember(Value = "GET")]
    Get,

    /// <summary>
    /// Use an HTTP HEAD request.
    /// </summary>
    [EnumMember(Value = "HEAD")]
    Head,
}
