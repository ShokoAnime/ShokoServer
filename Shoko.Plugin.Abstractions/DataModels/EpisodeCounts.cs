
namespace Shoko.Plugin.Abstractions.DataModels;

public class EpisodeCounts
{
    public int Episodes { get; set; }
    public int Specials { get; set; }
    public int Credits { get; set; }
    public int Trailers { get; set; }
    public int Others { get; set; }
    public int Parodies { get; set; }

    public int this[EpisodeType type] => type switch
    {
        EpisodeType.Episode => Episodes,
        EpisodeType.Special => Specials,
        EpisodeType.Credits => Credits,
        EpisodeType.Trailer => Trailers,
        EpisodeType.Parody => Parodies,
        _ => Others
    };
}
