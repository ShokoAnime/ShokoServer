using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Text;

/// <summary>
///   The title or overview chosen for an entry, and the step of the text
///   chooser that picked it.
/// </summary>
public sealed record TextChoiceInfo
{
    /// <summary>
    ///   Whether a title or an overview was chosen.
    /// </summary>
    public required TextKind Kind { get; init; }

    /// <summary>
    ///   The step of the chooser that picked the text.
    /// </summary>
    public required TextChoiceStep Step { get; init; }

    /// <summary>
    ///   The configured language the text was found in, for
    ///   <see cref="TextChoiceStep.LanguageOrder"/>,
    ///   <see cref="TextChoiceStep.GenericTitle"/>,
    ///   <see cref="TextChoiceStep.LanguagePreference"/> and the overview
    ///   fallbacks; otherwise <c>null</c>.
    /// </summary>
    public TitleLanguage? Language { get; init; }

    /// <summary>
    ///   The chosen text, or <c>null</c> when the entry has none.
    /// </summary>
    public IText? Text { get; init; }

    /// <summary>
    ///   The entry's default, which the chooser falls back on, or <c>null</c>
    ///   when it has none.
    /// </summary>
    public IText? Default { get; init; }
}
