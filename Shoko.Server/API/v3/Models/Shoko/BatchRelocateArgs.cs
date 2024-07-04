using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

#nullable enable
namespace Shoko.Server.API.v3.Models.Shoko;

public class BatchRelocateArgs
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
