using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Shoko.Abstractions.Metadata;

namespace Shoko.Server.Settings;

/// <summary>
/// How an anime's genres and studios are merged across its sources, part of
/// the <see cref="MetadataServiceSettings"/>.
/// </summary>
public class MetadataMergeSettings
{
    /// <summary>
    /// The most genres a merge accepts as a limit.
    /// </summary>
    public const int MaximumGenreLimit = 100;

    private List<MetadataSource> _sourceOrder = [MetadataSource.TMDB];

    /// <summary>
    /// The sources whose studios and genres are used, highest ranked first. A
    /// source not listed is not used, except AniDB, which is always used: at
    /// its place when listed, else first. Duplicates are dropped.
    /// </summary>
    [Display(Name = "Source Order")]
    public List<MetadataSource> SourceOrder
    {
        get => _sourceOrder;
        set => _sourceOrder = value.Distinct().ToList();
    }

    /// <summary>
    /// The most genres to give for an anime.
    /// </summary>
    [Display(Name = "Genre Limit")]
    [Range(0, MaximumGenreLimit)]
    public int GenreLimit { get; set; } = 5;

    /// <summary>
    /// The genres to leave out, such as those that only say "anime", matched
    /// by name ignoring case, spacing, punctuation and diacritics.
    /// </summary>
    [Display(Name = "Excluded Genres")]
    public List<string> ExcludedGenres { get; set; } = ["anime", "animation"];
}
