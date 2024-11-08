using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Models.CrossReference;
using static Shoko.Server.API.v3.Controllers.TmdbController;

#nullable enable
namespace Shoko.Server.API.v3.Models.TMDB.Input;

public class TmdbExportBody
{
    /// <summary>
    /// Include only cross-references with the given AniDB episode.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int? AnidbEpisodeID { get; set; } = null;

    /// <summary>
    /// Include only cross-references with the given AniDB anime.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int? AnidbAnimeID { get; set; } = null;

    /// <summary>
    /// Include only cross-references with the given TMDB show.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int? TmdbShowID { get; set; } = null;

    /// <summary>
    /// Include only cross-references with the given TMDB episode.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int? TmdbEpisodeID { get; set; } = null;

    /// <summary>
    /// Include only cross-references with the given TMDB movie.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int? TmdbMovieID { get; set; } = null;

    /// <summary>
    /// Include/exclude automatically made cross-references.
    /// </summary>
    [DefaultValue(IncludeOnlyFilter.True)]
    public IncludeOnlyFilter Automatic { get; set; } = IncludeOnlyFilter.True;

    /// <summary>
    /// Include/exclude cross-references with an episode. That is movie cross-references with an anidb episode set, or episode cross-references with a tmdb episode set.
    /// </summary>
    [DefaultValue(IncludeOnlyFilter.True)]
    public IncludeOnlyFilter WithEpisodes { get; set; } = IncludeOnlyFilter.True;

    /// <summary>
    /// Append human friendly comments in the output file. They serve no purpose other than to enlighten the humans reading the file what each cross-reference is for.
    /// </summary>
    public bool IncludeComments { get; set; } = false;

    /// <summary>
    /// Sections to include in the output file, if we have anything to fill in in the selected sections.
    /// </summary>
    public HashSet<CrossReferenceExportType>? SectionSet { get; set; } = null;

    /// <summary>
    /// Determines whether the movie filter is enabled.
    /// </summary>
    [JsonIgnore]
    public bool MovieFilerEnabled => AnidbAnimeID is not null || AnidbEpisodeID is not null || TmdbMovieID is not null;

    /// <summary>
    /// Determines whether the given movie cross-reference should be included in the export.
    /// </summary>
    /// <param name="xref">The movie cross-reference to check.</param>
    /// <returns><c>true</c> if the cross-reference should be included, <c>false</c> otherwise.</returns>
    public bool ShouldKeep(CrossRef_AniDB_TMDB_Movie xref)
    {
        if (!MovieFilerEnabled)
            return true;
        if (AnidbAnimeID is not null && AnidbAnimeID != xref.AnidbAnimeID)
            return false;
        if (AnidbEpisodeID is not null && AnidbEpisodeID != xref.AnidbEpisodeID)
            return false;
        if (TmdbMovieID is not null && TmdbMovieID != xref.TmdbMovieID)
            return false;
        return true;
    }

    /// <summary>
    /// Determines whether episode filtering is enabled.
    /// </summary>
    [JsonIgnore]
    public bool EpisodeFilterEnabled => AnidbAnimeID is not null || AnidbEpisodeID is not null || TmdbShowID is not null || TmdbEpisodeID is not null;

    /// <summary>
    /// Determines whether the given episode cross-reference should be included in the export.
    /// </summary>
    /// <param name="xref">The episode cross-reference to check.</param>
    /// <returns><c>true</c> if the cross-reference should be included, <c>false</c> otherwise.</returns>
    public bool ShouldKeep(CrossRef_AniDB_TMDB_Episode xref)
    {
        if (!EpisodeFilterEnabled)
            return true;
        if (AnidbAnimeID is not null && AnidbAnimeID != xref.AnidbAnimeID)
            return false;
        if (AnidbEpisodeID is not null && AnidbEpisodeID != xref.AnidbEpisodeID)
            return false;
        if (TmdbShowID is not null && TmdbShowID != xref.TmdbShowID)
            return false;
        if (TmdbEpisodeID is not null && TmdbEpisodeID != xref.TmdbEpisodeID)
            return false;
        return true;
    }
}
