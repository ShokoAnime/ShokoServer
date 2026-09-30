using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Server.API.v3.Models.TextManagement;

/// <summary>
///   The title or overview chosen for an entry, and the step of the chooser
///   that picked it.
/// </summary>
public class TextChoice
{
    /// <summary>
    ///   Whether a title or an overview was chosen.
    /// </summary>
    [Required]
    public TextKind Kind { get; set; }

    /// <summary>
    ///   The step of the chooser that picked the text.
    /// </summary>
    [Required]
    public TextChoiceStep Step { get; set; }

    /// <summary>
    ///   The configured language the text was found in, as a code such as
    ///   <c>x-main</c> or <c>en</c>, for <see cref="TextChoiceStep.LanguageOrder"/>,
    ///   <see cref="TextChoiceStep.GenericTitle"/> and
    ///   <see cref="TextChoiceStep.LanguagePreference"/>; otherwise <c>null</c>.
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    ///   The source the chosen text is from, or <c>null</c> when nothing was
    ///   chosen.
    /// </summary>
    public MetadataSource? Source { get; set; }

    /// <summary>
    ///   The chosen text, or <c>null</c> when the entry has none.
    /// </summary>
    public ManagedText? Text { get; set; }

    /// <summary>
    ///   The entry's default, which the chooser falls back on, or <c>null</c>
    ///   when it has none.
    /// </summary>
    public ManagedText? Default { get; set; }
}

/// <summary>
///   The steps of the chooser, in the order it takes them.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum TextChoiceStep
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
    ///   A made-up generic title, for an episode no source named.
    /// </summary>
    Synthesized = 6,
}
