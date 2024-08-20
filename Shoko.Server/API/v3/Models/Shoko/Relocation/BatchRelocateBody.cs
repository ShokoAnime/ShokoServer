using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

#pragma warning disable CS8618
#nullable enable
namespace Shoko.Server.API.v3.Models.Shoko.Relocation;

public class BatchRelocateBody
{
    /// <summary>
    /// The file IDs to preview
    /// </summary>
    [Required]
    public IEnumerable<int> FileIDs { get; set; }

    /// <summary>
    /// The config to use. If null, the default config will be used
    /// </summary>
    public RenamerConfig? Config { get; set; }
}
