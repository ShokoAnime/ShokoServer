using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Text;

namespace Shoko.Server.API.v3.Models.TextManagement;

/// <summary>
///   The title or overview chosen for an entry, and the step of the chooser
///   that picked it.
/// </summary>
/// <param name="choice">The choice.</param>
public class TextChoice(TextChoiceInfo choice)
{
    /// <summary>
    ///   Whether a title or an overview was chosen.
    /// </summary>
    [Required]
    public TextKind Kind { get; init; } = choice.Kind;

    /// <summary>
    ///   The step of the chooser that picked the text.
    /// </summary>
    [Required]
    public TextChoiceStep Step { get; init; } = choice.Step;

    /// <summary>
    ///   The configured language the text was found in, as a code such as
    ///   <c>x-main</c> or <c>en</c>, for <see cref="TextChoiceStep.LanguageOrder"/>,
    ///   <see cref="TextChoiceStep.GenericTitle"/>,
    ///   <see cref="TextChoiceStep.LanguagePreference"/> and the overview
    ///   fallbacks; otherwise <c>null</c>.
    /// </summary>
    public string? Language { get; init; } = choice.Language?.GetString();

    /// <summary>
    ///   The source the chosen text is from, or <c>null</c> when nothing was
    ///   chosen.
    /// </summary>
    public MetadataSource? Source { get; init; } = choice.Text?.Source;

    /// <summary>
    ///   The chosen text, or <c>null</c> when the entry has none.
    /// </summary>
    public ManagedText? Text { get; init; } = choice.Text is { } text ? new ManagedText(text) : null;

    /// <summary>
    ///   The entry's default, which the chooser falls back on, or <c>null</c>
    ///   when it has none.
    /// </summary>
    public ManagedText? Default { get; init; } = choice.Default is { } defaultText ? new ManagedText(defaultText) : null;
}
