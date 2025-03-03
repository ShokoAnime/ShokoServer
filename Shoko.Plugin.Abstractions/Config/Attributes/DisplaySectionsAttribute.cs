
using System;
using System.Collections.Generic;
using System.Linq;
using NJsonSchema.Annotations;

namespace Shoko.Plugin.Abstractions.Config.Attributes;

/// <summary>
/// Define display sections for the the object in the UI.
/// </summary>
/// <param name="sections">The sections. Order matters.</param>
[AttributeUsage(AttributeTargets.Class)]
public class DisplaySectionsAttribute(string[] sections) : JsonSchemaExtensionDataAttribute("x-ui-display-sections", sections.ToDictionary(name => name.ToLower().Replace(' ', '-'))) { }
