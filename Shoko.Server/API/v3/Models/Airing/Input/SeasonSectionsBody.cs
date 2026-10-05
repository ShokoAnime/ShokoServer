using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Abstractions.Metadata.Enums;

#nullable enable
namespace Shoko.Server.API.v3.Models.Airing.Input;

/// <summary>
/// A custom layout for the season view.
/// </summary>
public class SeasonSectionsBody
{
    /// <summary>
    /// The sections, in order. Each anime goes to the first that takes it.
    /// </summary>
    [Required]
    public List<Section> Sections { get; set; } = [];

    /// <summary>
    /// One section of the layout.
    /// </summary>
    public class Section
    {
        /// <summary>
        /// The section's ID, unique within the layout.
        /// </summary>
        [Required, MinLength(1)]
        public string ID { get; set; } = string.Empty;

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
        /// The section as the airing calendar service takes it.
        /// </summary>
        /// <returns>The definition.</returns>
        public SeasonSectionDefinition ToDefinition()
            => new()
            {
                ID = ID,
                Title = Title,
                Types = Types,
                Continuing = Continuing,
                HalfLength = HalfLength,
            };
    }
}
