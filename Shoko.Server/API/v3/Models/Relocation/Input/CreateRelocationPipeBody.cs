
#nullable enable
using System;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json.Linq;

namespace Shoko.Server.API.v3.Models.Relocation.Input;

public class CreateRelocationPipeBody
{
    [Required]
    public Guid ProviderID { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public bool IsDefault { get; set; } = false;

    public JObject? Configuration { get; set; } = null;
}
