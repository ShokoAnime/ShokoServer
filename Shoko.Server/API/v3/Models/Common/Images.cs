#nullable enable
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Shoko.Server.API.v3.Models.Common;

public class Images
{
    [Required]
    public List<Image> Posters { get; set; } = [];

    [Required]
    public List<Image> Backdrops { get; set; } = [];

    [Required]
    public List<Image> Banners { get; set; } = [];

    [Required]
    public List<Image> Logos { get; set; } = [];

    [Required]
    public List<Image> Discs { get; set; } = [];
}
