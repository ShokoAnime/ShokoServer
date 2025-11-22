using System;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json.Linq;

#nullable enable
namespace Shoko.Server.API.v3.Models.Relocation.Input;

/// <summary>
///   Represents the information required to create a new relocation pipe.
/// </summary>
public class CreateRelocationPipeBody
{
    /// <summary>
    ///   The provider ID.
    /// </summary>
    [Required]
    public Guid ProviderID { get; set; }

    /// <summary>
    ///   The pipe name. Cannot be empty. If it is a duplicate, the name will be
    ///   shifted to the next available copy number.
    /// </summary>
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///   Whether or not to make the new pipe the default pipe.
    /// </summary>
    public bool IsDefault { get; set; } = false;

    /// <summary>
    ///   Optional. The configuration for the pipe if the provider requires one.
    ///   If omitted, then a new configuration will be generated instead.
    /// </summary>
    public JObject? Configuration { get; set; } = null;
}
