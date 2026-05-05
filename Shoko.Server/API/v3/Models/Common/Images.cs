using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

#nullable enable
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

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public List<Image>? Thumbnails { get; set; } = null;
}
