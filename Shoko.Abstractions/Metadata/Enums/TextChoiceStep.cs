using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Metadata.Enums;

/// <summary>
///   The steps the text chooser takes for an entry's preferred title or
///   overview, in the order it takes them.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum TextChoiceStep : byte
{
    /// <summary>
    ///   The entry has no text of the kind at all.
    /// </summary>
    None = 0,

    /// <summary>
    ///   A user's overall pick, which beats every other text.
    /// </summary>
    OverallPreference = 1,

    /// <summary>
    ///   A user's pick for the text's language, once the language order
    ///   reached it.
    /// </summary>
    LanguagePreference = 2,

    /// <summary>
    ///   The first text found walking the configured languages and, in each,
    ///   the sources in their order.
    /// </summary>
    /// <remarks>
    ///   For a Shoko series' overview, each source answers from the entry
    ///   that fits the anime best: the whole show when every regular season
    ///   is linked, else the lowest linked season. AniDB's description is
    ///   passed over while it holds only notes.
    /// </remarks>
    LanguageOrder = 3,

    /// <summary>
    ///   A generic episode title such as <c>Episode 5</c>, taken because no
    ///   real title was found in any configured language.
    /// </summary>
    GenericTitle = 4,

    /// <summary>
    ///   The entry's default, since nothing was found in a configured
    ///   language.
    /// </summary>
    Default = 5,

    /// <summary>
    ///   A synthesized generic title, for an episode no source named.
    /// </summary>
    Synthesized = 6,

    /// <summary>
    ///   A Shoko series' overview read off the show, at once, as the linked
    ///   first season had none in the language.
    /// </summary>
    FirstSeasonFallback = 7,

    /// <summary>
    ///   A Shoko series' overview read off the show, as the linked later
    ///   season had none in the language and no source's best entry had one.
    /// </summary>
    ShowFallback = 8,

    /// <summary>
    ///   AniDB's description holding only notes, such as what the anime is
    ///   based on, taken as the last resort in the language.
    /// </summary>
    NoteFallback = 9,
}
