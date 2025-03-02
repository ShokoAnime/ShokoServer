using Shoko.Plugin.Abstractions.Config.Attributes;

namespace Shoko.Plugin.Abstractions.Enums;

/// <summary>
/// Coding languages for <see cref="RenamerSettingType.Code"/> and
/// <see cref="CodeBlockAttribute"/>
/// </summary>
public enum CodeLanguage
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
