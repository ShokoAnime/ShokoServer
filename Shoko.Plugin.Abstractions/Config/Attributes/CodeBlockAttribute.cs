using System;
using System.Collections.Generic;
using NJsonSchema.Annotations;
using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.Config.Attributes;

/// <summary>
/// Used to mark a property as a code block in the UI.
/// </summary>
/// <param name="language">The code language for the editor. </param>
[AttributeUsage(AttributeTargets.Property)]
public class CodeBlockAttribute(CodeLanguage language) : JsonSchemaExtensionDataAttribute("x-ui-element", new Dictionary<string, string>() { { "element-type", "code-block" }, { "language", language.ToString() } }) { }
