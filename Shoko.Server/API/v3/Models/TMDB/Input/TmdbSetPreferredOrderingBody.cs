using System.ComponentModel.DataAnnotations;
using Shoko.Server.API.v3.Controllers;

namespace Shoko.Server.API.v3.Models.TMDB.Input;

public class TmdbSetPreferredOrderingBody
{
    /// <summary>
    /// The new preferred ordering to use: the ID of one of the show's episode
    /// group collections, or <c>default</c> (or the show's own ID) to go back
    /// to the show's default ordering.
    /// </summary>
    [Required]
    [RegularExpression(TmdbController.AlternateOrderingIdRegex)]
    public string AlternateOrderingID { get; set; } = string.Empty;
}
