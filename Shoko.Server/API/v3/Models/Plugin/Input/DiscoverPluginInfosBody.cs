
#nullable enable
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Shoko.Server.API.v3.Models.Plugin.Input;

public class DiscoverPluginInfosBody
{
    /// <summary>
    ///   The paths to attempt to load. If relative, it will be first checked if
    ///   it's relative to the system plugin directory followed by the user
    ///   plugin directory. If absolute, it will be checked if it lies within
    ///   one of the two mentioned directories.
    /// </summary>
    [Required]
    [MinLength(1)]
    public IReadOnlyList<string> Paths { get; set; } = [];
}
