using System.Collections.Generic;
using Shoko.Server.Providers.AniDB.HTTP.GetAnime;

namespace Shoko.Server.Providers.AniDB.HTTP;

public class ResponseGetAnime
{
    public ResponseAnime Anime { get; set; } = null!;
    public List<ResponseTitle> Titles { get; set; } = null!;
    public List<ResponseEpisode> Episodes { get; set; } = null!;
    public List<ResponseTag> Tags { get; set; } = null!;
    public List<ResponseStaff> Staff { get; set; } = null!;
    public List<ResponseCharacter> Characters { get; set; } = null!;
    public List<ResponseResource> Resources { get; set; } = null!;
    public List<ResponseRelation> Relations { get; set; } = null!;
    public List<ResponseSimilar> Similar { get; set; } = null!;
}
