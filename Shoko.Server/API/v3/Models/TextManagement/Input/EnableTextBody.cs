using System.ComponentModel.DataAnnotations;

namespace Shoko.Server.API.v3.Models.TextManagement.Input;

/// <summary>
///   Body for enabling or disabling a stored text.
/// </summary>
public class EnableTextBody
{
    /// <summary>
    ///   Whether the text may be listed and chosen.
    /// </summary>
    [Required]
    public bool Enabled { get; set; }
}
