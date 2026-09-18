using System.ComponentModel.DataAnnotations;

namespace Shoko.Server.API.v3.Models.Streaming.Input;

public class UpdateMultipleTransformsBody
{
    [Required]
    public string ID { get; set; } = string.Empty;

    public int? Priority { get; set; }

    public bool? IsEnabled { get; set; }
}
