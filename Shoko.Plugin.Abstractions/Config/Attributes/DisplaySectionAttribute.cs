
using System;
using System.Collections.Generic;
using NJsonSchema.Annotations;

namespace Shoko.Plugin.Abstractions.Config.Attributes;

/// <summary>
/// Define which section a property belongs to in the UI.
/// </summary>
/// <param name="name">The name of the section.</param>
[AttributeUsage(AttributeTargets.Property)]
public class DisplaySectionAttribute(string name) : JsonSchemaExtensionDataAttribute("x-ui-display-section", new Dictionary<string, string>() { { "key", name.ToLower().Replace(' ', '-') }, { "name", name } }) { }
