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

    /// <summary>
    /// How the editor reads a script: as a plain script, a module or a function body. Languages
    /// that run no code ignore it.
    /// </summary>
    public CodeEditorScriptKind ScriptKind { get; init; }

    /// <summary>
    /// The server path of the definitions file the editor loads for completion and checking in
    /// the browser, fetched with the user's credentials, or <c>null</c> for none. Its kind
    /// follows the <see cref="Language"/>: TypeScript declarations (<c>.d.ts</c>) for JavaScript
    /// and TypeScript, a JSON Schema for JSON and YAML, a Lua definitions file (<c>---@meta</c>)
    /// for Lua, an XML Schema for XML.
    /// </summary>
    public string? DefinitionsPath { get; init; }
}
