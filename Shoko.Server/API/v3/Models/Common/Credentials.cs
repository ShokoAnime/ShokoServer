using System.ComponentModel.DataAnnotations;

namespace Shoko.Server.API.v3.Models.Common;

public class Credentials
{
    /// <summary>
    /// A generic Username field
    /// </summary>
    [Required]
    public string Username { get; set; }

    /// <summary>
    /// A generic password field
    /// </summary>
    [Required(AllowEmptyStrings = true)]
    public string Password { get; set; }
}
