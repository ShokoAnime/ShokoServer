using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using static Shoko.Server.API.v3.Controllers.TmdbController;

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
    /// Determines whether episode filtering is enabled.
    /// </summary>
    [JsonIgnore]
    public bool EpisodeFilterEnabled => AnidbAnimeID is not null || AnidbEpisodeID is not null || TmdbShowID is not null || TmdbEpisodeID is not null;


    /// <summary>
    /// Determines whether the show filter is enabled.
    /// </summary>
    [JsonIgnore]
    public bool ShowFilterEnabled => AnidbAnimeID is not null || TmdbShowID is not null;

    /// <summary>
    /// The export options this body asks for.
    /// </summary>
    /// <returns>The options for the cross-reference transfer service.</returns>
    public MetadataCrossReferenceExportOptions ToOptions()
    {
        var sections = SectionSet?.CombineFlags() ?? CrossReferenceExportType.None;
        return new()
        {
            Sections = (sections.HasFlag(CrossReferenceExportType.Movie) ? MetadataCrossReferenceSections.Movie : MetadataCrossReferenceSections.None) |
                (sections.HasFlag(CrossReferenceExportType.Show) ? MetadataCrossReferenceSections.Series : MetadataCrossReferenceSections.None) |
                (sections.HasFlag(CrossReferenceExportType.Episode) ? MetadataCrossReferenceSections.Episode : MetadataCrossReferenceSections.None),
            AnidbAnimeID = AnidbAnimeID,
            AnidbEpisodeID = AnidbEpisodeID,
            ProviderSeriesID = TmdbShowID?.ToString(),
            ProviderEpisodeID = TmdbEpisodeID?.ToString(),
            ProviderMovieID = TmdbMovieID?.ToString(),
            Automatic = ToFilter(Automatic),
            WithEpisodes = ToFilter(WithEpisodes),
            IncludeComments = IncludeComments,
        };
    }

    /// <summary>
    /// A three-way include filter as the transfer service takes it.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <returns><see langword="null"/> for both, <see langword="true"/> for only, <see langword="false"/> for none.</returns>
    private static bool? ToFilter(IncludeOnlyFilter filter)
        => filter switch
        {
            IncludeOnlyFilter.Only => true,
            IncludeOnlyFilter.False => false,
            _ => null,
        };
}
