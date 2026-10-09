using System.Runtime.Serialization;
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
    [EnumMember(Value = "plain-text")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("plain-text")]
    PlainText = 0,

    /// <summary>
    /// C#.
    /// </summary>
    [EnumMember(Value = "csharp")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("csharp")]
    CSharp = 1,

    /// <summary>
    /// Java.
    /// </summary>
    [EnumMember(Value = "java")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("java")]
    Java = 2,

    /// <summary>
    /// JavaScript.
    /// </summary>
    [EnumMember(Value = "javascript")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("javascript")]
    JavaScript = 3,

    /// <summary>
    /// TypeScript.
    /// </summary>
    [EnumMember(Value = "typescript")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("typescript")]
    TypeScript = 4,

    /// <summary>
    /// Lua.
    /// </summary>
    [EnumMember(Value = "lua")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("lua")]
    Lua = 5,

    /// <summary>
    /// Python.
    /// </summary>
    [EnumMember(Value = "python")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("python")]
    Python = 6,

    /// <summary>
    /// INI.
    /// </summary>
    [EnumMember(Value = "ini")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("ini")]
    Ini = 7,

    /// <summary>
    /// JSON.
    /// </summary>
    [EnumMember(Value = "json")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("json")]
    Json = 8,

    /// <summary>
    /// YAML.
    /// </summary>
    [EnumMember(Value = "yaml")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("yaml")]
    Yaml = 9,

    /// <summary>
    /// XML.
    /// </summary>
    [EnumMember(Value = "xml")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("xml")]
    Xml = 10,
}
