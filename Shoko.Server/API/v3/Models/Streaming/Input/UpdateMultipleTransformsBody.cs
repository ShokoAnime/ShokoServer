using System;
using System.ComponentModel.DataAnnotations;

namespace Shoko.Server.API.v3.Models.Streaming.Input;

public class UpdateMultipleTransformsBody
{
    [Required]
    public Guid ID { get; set; }

    public int? Priority { get; set; }

    public bool? IsEnabled { get; set; }
}
