using System.ComponentModel.DataAnnotations;

#nullable enable
namespace Shoko.Server.API.v3.Models.Shoko;

public class ChildItems
{
    [Required]
    public int Folders { get; set; }

    [Required]
    public int Files { get; set; }
}
