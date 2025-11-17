using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json.Linq;

#nullable enable
namespace Shoko.Server.API.v3.Models.Relocation.Input;

public class BatchRelocatePreviewBody
{
    /// <summary>
    /// The file IDs to preview
    /// </summary>
    [Required]
    public IEnumerable<int> FileIDs { get; set; } = [];

    /// <summary>
    /// The provider ID.
    /// </summary>
    public Guid? ProviderID { get; set; }

    /// <summary>
    /// The configuration.
    /// </summary>
    public JObject? Configuration { get; set; }
}
