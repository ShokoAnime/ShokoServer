using System.ComponentModel.DataAnnotations;

namespace Shoko.Server.API.v3.Models.Auth;

/// <summary>
/// Request to generate a new API token.
/// </summary>
public class GenerateTokenRequest
{
    /// <summary>
    /// The device name.
    /// </summary>
    [Required(ErrorMessage = "Device is required")]
    public string Device { get; set; } = null!;

    /// <summary>
    /// Optional token expiration. Accepted formats:
    ///
    /// - A simple duration string (e.g. `"7d"`, `"24h"`, `"30m"`)
    /// - An ISO 8601 duration (e.g. `"P7D"`, `"PT24H"`)
    /// - An ISO 8601 absolute datetime (e.g. `"2026-07-01T00:00:00Z"`)
    ///
    /// If omitted or null, the token will not expire.
    /// </summary>
    public string? Expires { get; set; }
}
