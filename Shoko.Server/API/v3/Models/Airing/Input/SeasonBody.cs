using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Abstractions.Metadata.Enums;

#nullable enable
namespace Shoko.Server.API.v3.Models.Airing.Input;

/// <summary>
/// The body every season <c>POST</c> route takes: a layout for the season
/// view, which also decides which anime a season lists and counts, and a
/// filter to narrow it by.
/// </summary>
public class SeasonBody : AiringFilterBody
{
    /// <summary>
    /// The sections, in order, or <c>null</c> for the default layout. Each
    /// anime goes to the first that takes it, and an anime none takes is
    /// left out of the season's lists and counts.
    /// </summary>
    public List<Section>? Sections { get; set; }

    /// <summary>
    /// One section of the layout.
    /// </summary>
    public class Section
    {
        /// <summary>
        /// The section's heading.
        /// </summary>
        [Required]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// The anime types it takes, or <c>null</c> for the rest group: every
        /// type, so whatever no earlier section took.
        /// </summary>
        public HashSet<AnimeType>? Types { get; set; }

        /// <summary>
        /// <c>true</c> for only the anime that started before the season,
        /// <c>false</c> for only the new ones, <c>null</c> for both.
        /// </summary>
        public bool? Continuing { get; set; }

        /// <summary>
        /// <c>true</c> for only the anime whose episodes run under 16
        /// minutes, <c>false</c> for only the others, <c>null</c> for both.
        /// </summary>
        public bool? HalfLength { get; set; }

        /// <summary>
        /// The section as the airing calendar service and the anime catalog
        /// take it.
        /// </summary>
        /// <returns>The definition.</returns>
        public SeasonSectionDefinition ToDefinition()
            => new()
            {
                Title = Title,
                Types = Types,
                Continuing = Continuing,
                HalfLength = HalfLength,
            };

        /// <summary>
        /// The section as a layout sends it, from its definition.
        /// </summary>
        /// <param name="definition">The definition.</param>
        /// <returns>The section.</returns>
        public static Section FromDefinition(SeasonSectionDefinition definition)
            => new()
            {
                Title = definition.Title,
                Types = definition.Types is { } types ? [.. types] : null,
                Continuing = definition.Continuing,
                HalfLength = definition.HalfLength,
            };
    }
}
