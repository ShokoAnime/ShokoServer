using System;
using Shoko.Abstractions.UI.Enums;

namespace Shoko.Abstractions.UI.Attributes;

/// <summary>
/// Used to mark a property/field as a code editor in the specified language in
/// the UI.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class CodeEditorAttribute : Attribute
{
    /// <summary>
    /// The code language for the editor to use.
    /// </summary>
    public CodeEditorLanguage Language { get; set; }

    /// <summary>
    /// Whether to automatically format the code/text on load.
    /// </summary>
    public bool AutoFormatOnLoad { get; set; }

    /// <summary>
    /// How the editor reads a script: as a plain script, a module or a function body. Languages
    /// that run no code ignore it.
    /// </summary>
    public CodeEditorScriptKind ScriptKind { get; set; }

    /// <summary>
    /// The server path of a definitions file the editor loads for completion and checking in
    /// the browser, fetched with the user's credentials, e.g.
    /// <c>/api/plugin/MyPlugin/script.d.ts</c>; <c>null</c> for none. Its kind follows the
    /// <see cref="Language"/>: TypeScript declarations (<c>.d.ts</c>) for JavaScript and
    /// TypeScript, a JSON Schema for JSON and YAML, a Lua definitions file (<c>---@meta</c>)
    /// for Lua, an XML Schema for XML. A language the editor checks nothing for ignores it.
    /// </summary>
    public string? DefinitionsPath { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CodeEditorAttribute"/> class with the specified <see cref="CodeEditorLanguage"/>.
    /// </summary>
    /// <param name="language">The code language for the editor to use.</param>
    public CodeEditorAttribute(CodeEditorLanguage language) => Language = language;
}
