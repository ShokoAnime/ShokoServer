using Shoko.Abstractions.UI.Enums;

namespace Shoko.Abstractions.UI.Elements;

/// <summary>
/// A syntax-highlighted code editor.
/// </summary>
public sealed class UiCodeEditorElement : UiTextElement
{
    /// <inheritdoc />
    public override UiElementKind Kind => UiElementKind.CodeEditor;

    /// <summary>
    /// The language to highlight the content as.
    /// </summary>
    public CodeEditorLanguage Language { get; init; }

    /// <summary>
    /// Whether the client should reformat the content when it is first loaded.
    /// </summary>
    public bool AutoFormatOnLoad { get; init; }
}
