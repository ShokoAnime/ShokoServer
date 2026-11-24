using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Metadata.Enums;

/// <summary>
///   What kind of special or extra an episode is, as its source says, beside
///   the coarser <see cref="EpisodeType"/>: story kinds (part of the story,
///   though not a regular episode) and extras (about the show rather than part
///   of it) alike. <see cref="None"/> for every regular episode and wherever
///   the source says nothing.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum ExtraType : byte
{
    /// <summary>
    ///   The source gives no kind.
    /// </summary>
    None = 0,

    /// <summary>
    ///   A special that is part of the story, such as an OVA or an OAD. A
    ///   story kind.
    /// </summary>
    StoryEpisode = 1,

    /// <summary>
    ///   A short episode, bonus shorts (omake) included. A story kind.
    /// </summary>
    Short = 2,

    /// <summary>
    ///   A recap of earlier episodes.
    /// </summary>
    Recap = 3,

    /// <summary>
    ///   A preview of an episode to come.
    /// </summary>
    Preview = 4,

    /// <summary>
    ///   A trailer, promo or commercial.
    /// </summary>
    Trailer = 5,

    /// <summary>
    ///   A commentary.
    /// </summary>
    Commentary = 6,

    /// <summary>
    ///   A short clip shown in cinemas before the film, such as a
    ///   theater-manners clip (マナー映像), or a greeting before a screening.
    /// </summary>
    CinemaIntro = 7,

    /// <summary>
    ///   A talk show.
    /// </summary>
    TalkShow = 8,

    /// <summary>
    ///   A live event, such as a stage event, a concert or a reunion.
    /// </summary>
    LiveEvent = 9,

    /// <summary>
    ///   An interview.
    /// </summary>
    Interview = 10,

    /// <summary>
    ///   Behind the scenes, making-of included.
    /// </summary>
    BehindTheScenes = 11,

    /// <summary>
    ///   A film. A story kind.
    /// </summary>
    Movie = 12,

    /// <summary>
    ///   An episode made for the web. A story kind.
    /// </summary>
    Webisode = 13,

    /// <summary>
    ///   Bloopers.
    /// </summary>
    Bloopers = 14,

    /// <summary>
    ///   A deleted scene.
    /// </summary>
    DeletedScene = 15,

    /// <summary>
    ///   A special or extra of none of the other kinds.
    /// </summary>
    Other = 255,
}
