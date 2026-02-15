using System.ComponentModel.DataAnnotations;

#nullable enable
namespace Shoko.Server.API.v0.Models;

public class AuthUser
{
    [Required(ErrorMessage = "Username is required")]
    public string user { get; set; } = null!;

    [Required(ErrorMessage = "Password is required", AllowEmptyStrings = true)]
    public string pass { get; set; } = null!;

    [Required(ErrorMessage = "Device is required")]
    public string device { get; set; } = null!;
}
