using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

#nullable enable
namespace Shoko.Server.API.v3.Models.Hashing.Input;

public class UpdateMultipleProvidersBody
{
    [Required]
    public Guid ID { get; set; }

    public int? Priority { get; set; }

    public HashSet<string>? EnabledHashTypes { get; set; }
}
