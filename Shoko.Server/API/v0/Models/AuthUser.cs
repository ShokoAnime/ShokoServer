using System.ComponentModel.DataAnnotations;

namespace Shoko.Server.API.v0.Models;

public class AuthUser
{
    [Required(ErrorMessage = "Username is required")]
    public string user { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required", AllowEmptyStrings = true)]
    public string pass { get; set; } = string.Empty;

    [Required(ErrorMessage = "Device is required")]
    public string device { get; set; } = string.Empty;

    /// <summary>
    /// Optional token expiration. Accepted formats:
    ///
    /// - A simple duration string (e.g. `"7d"`, `"24h"`, `"30m"`)
    /// - An ISO 8601 duration (e.g. `"P7D"`, `"PT24H"`)
    /// - An ISO 8601 absolute datetime (e.g. `"2026-07-01T00:00:00Z"`)
    ///
    /// If omitted or null, the token will not expire.
    /// </summary>
    public string? expires { get; set; }
}
