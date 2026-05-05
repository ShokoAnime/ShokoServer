
using System.ComponentModel.DataAnnotations;

namespace Shoko.Server.API.v3.Models.AniDB;

/// <summary>
/// The result entries for the "Recommended For You" algorithm.
/// </summary>
public class AnidbAnimeRecommendedForYou
{
    /// <summary>
    /// The recommended AniDB entry.
    /// </summary>
    [Required]
    public AnidbAnime Anime { get; init; }

    /// <summary>
    /// Number of similar anime that resulted in this recommendation.
    /// </summary>
    [Required]
    public int SimilarTo { get; init; }

    public AnidbAnimeRecommendedForYou(AnidbAnime anime, int similarCount)
    {
        Anime = anime;
        SimilarTo = similarCount;
    }
}
