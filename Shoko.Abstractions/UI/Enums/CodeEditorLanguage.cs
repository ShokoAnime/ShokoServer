using Shoko.Abstractions.UI.Attributes;

namespace Shoko.Abstractions.UI.Enums;

/// <summary>
/// Coding languages for <see cref="CodeEditorAttribute"/>.
/// </summary>
[Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum CodeEditorLanguage
{
    /// <summary>
    /// Plain text.
    /// </summary>
    PlainText = 0,

    /// <summary>
    /// C#.
    /// </summary>
    CSharp = 1,

    /// <summary>
    /// Java.
    /// </summary>
    Java = 2,

    /// <summary>
    /// JavaScript.
    /// </summary>
    JavaScript = 3,

    /// <summary>
    /// TypeScript.
    /// </summary>
    TypeScript = 4,

    /// <summary>
    /// Lua.
    /// </summary>
    Lua = 5,

    /// <summary>
    /// Python.
    /// </summary>
    Python = 6,

    /// <summary>
    /// INI.
    /// </summary>
    Ini = 7,

    /// <summary>
    /// JSON.
    /// </summary>
    Json = 8,

    /// <summary>
    /// YAML.
    /// </summary>
    Yaml = 9,

    /// <summary>
    /// XML.
    /// </summary>
    Xml = 10,
}
