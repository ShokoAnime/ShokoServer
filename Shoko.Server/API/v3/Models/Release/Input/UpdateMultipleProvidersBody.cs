using System;
using System.ComponentModel.DataAnnotations;

#nullable enable
namespace Shoko.Server.API.v3.Models.Release.Input;

public class UpdateMultipleProvidersBody
{
    [Required]
    public Guid ID { get; set; }

    public int? Priority { get; set; }

    public bool? IsEnabled { get; set; }
}
