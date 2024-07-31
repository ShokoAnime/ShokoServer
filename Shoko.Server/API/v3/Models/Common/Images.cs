using System.Collections.Generic;
using Newtonsoft.Json;

#nullable enable
namespace Shoko.Server.API.v3.Models.Common;

public class Images
{
    public List<Image> Posters { get; set; } = [];
    public List<Image> Backdrops { get; set; } = [];
    public List<Image> Banners { get; set; } = [];
    public List<Image> Logos { get; set; } = [];

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public List<Image>? Thumbnails { get; set; } = null;
}
