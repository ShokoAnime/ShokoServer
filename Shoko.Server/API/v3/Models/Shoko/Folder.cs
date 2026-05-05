using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

#nullable enable
namespace Shoko.Server.API.v3.Models.Shoko;

public class Folder
{
    [Required]
    public string Path { get; set; } = string.Empty;

    [Required, DefaultValue(false)]
    public bool IsAccessible { get; set; }

    public ChildItems? Sizes { get; set; }
}
