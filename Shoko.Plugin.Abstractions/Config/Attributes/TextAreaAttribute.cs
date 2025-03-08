
using System.Collections.Generic;
using NJsonSchema.Annotations;

namespace Shoko.Plugin.Abstractions.Config.Attributes;

/// <summary>
/// Used to mark a property as a text-area in the UI.
/// </summary>
public class TextAreaAttribute() : JsonSchemaExtensionDataAttribute("x-ui-element", new Dictionary<string, string>() { { "element-type", "text-area" } }) { }
