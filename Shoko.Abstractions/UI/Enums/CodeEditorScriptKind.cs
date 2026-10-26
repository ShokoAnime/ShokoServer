using System.Runtime.Serialization;
using Shoko.Abstractions.UI.Attributes;

namespace Shoko.Abstractions.UI.Enums;

/// <summary>
/// How the editor of <see cref="CodeEditorAttribute"/> reads a script, for languages that run
/// code. A data language (JSON, YAML, INI, XML) and plain text ignore it.
/// </summary>
[Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum CodeEditorScriptKind
{
    /// <summary>
    /// A plain script: top-level code with no top-level <c>return</c> and no exports, which
    /// gives its results through the globals the host defines (its definitions file declares
    /// them). A classic script in JavaScript and TypeScript.
    /// </summary>
    [EnumMember(Value = "script")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("script")]
    Script = 0,

    /// <summary>
    /// A module: what it gives is what it exports, and top-level code returns nothing. An ES module
    /// in JavaScript and TypeScript: <c>import</c> and <c>export</c>, no top-level <c>return</c>.
    /// </summary>
    [EnumMember(Value = "module")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("module")]
    Module = 1,

    /// <summary>
    /// The body of a function: top-level <c>return</c> gives the result, and the language's
    /// top-level <c>await</c> where it has one. An async function body in JavaScript and
    /// TypeScript.
    /// </summary>
    [EnumMember(Value = "function-body")]
    [System.Text.Json.Serialization.JsonStringEnumMemberName("function-body")]
    FunctionBody = 2,
}
