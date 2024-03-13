using System.Collections.Generic;

namespace Shoko.Server.API.v3.Models.Common;

public class Images
{
    public List<Image> Posters { get; set; } = new();
    public List<Image> Backdrops { get; set; } = new();
    public List<Image> Banners { get; set; } = new();
    public List<Image> Logos { get; set; } = new();
}
