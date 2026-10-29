using System;
using System.ComponentModel.DataAnnotations;

namespace Shoko.Abstractions.Web;

/// <summary>
///   Serves another web UI than the default one to the requests for a host.
/// </summary>
public class WebUIHostOverride
{
    /// <summary>
    ///   The host the requests are for: a host name or IP address with an
    ///   optional port, such as <c>media.example.com</c> or
    ///   <c>192.168.1.10:8111</c>, and an IPv6 address in brackets. Compared
    ///   ignoring case; an entry with a port beats one without.
    /// </summary>
    [Key]
    public required string Host { get; init; }

    /// <summary>
    ///   The ID of the web UI to serve for the host.
    /// </summary>
    public required Guid ProviderID { get; init; }
}
