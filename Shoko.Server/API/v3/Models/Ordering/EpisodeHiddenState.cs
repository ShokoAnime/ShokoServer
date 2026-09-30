using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Shoko.Server.API.v3.Models.Ordering;

/// <summary>
/// Whether a user hid an episode, of any source.
/// </summary>
public class EpisodeHiddenState
{
    /// <summary>
    /// The episode's full ID, e.g. <c>anidb://episode/1</c>.
    /// </summary>
    [Required]
    public string EpisodeID { get; init; } = string.Empty;

    /// <summary>
    /// Whether the episode is hidden.
    /// </summary>
    [Required]
    public bool IsHidden { get; init; }

    /// <summary>
    /// The bodies the hidden state endpoints take.
    /// </summary>
    public static class Input
    {
        /// <summary>
        /// Hides or shows an episode.
        /// </summary>
        public class SetHiddenBody
        {
            /// <summary>
            /// The episode's full ID, e.g. <c>anidb://episode/1</c>.
            /// </summary>
            [Required]
            public string EpisodeID { get; set; } = string.Empty;

            /// <summary>
            /// Whether to hide it. Defaults to <c>true</c>.
            /// </summary>
            [DefaultValue(true)]
            public bool Value { get; set; } = true;
        }
    }
}
