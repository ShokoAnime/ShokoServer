using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.UI.Enums;

namespace Shoko.Server.Services.Configuration;

/// <summary>
///   Legacy compatibility, to be removed: writes back the few
///   <c>x-uiDefinition</c> entries the released web UI's dynamic form reads
///   off a configuration's schema, for renamer presets and metadata providers.
///   Everything else reads <see cref="Abstractions.UI.UiDefinition"/> instead.
/// </summary>
/// <remarks>
///   Removing it is deleting this file and its one call in
///   <see cref="ShokoJsonSchemaGenerator.GetSchemaForType"/>.
/// </remarks>
internal static class LegacySchemaUiCompat
{
    private const string SectionType = "sectionType";

    private const string Structure = "structure";

    private const string StructureProperty = "property";

    private const string CodeLanguage = "codeLanguage";

    /// <summary>
    ///   Adds the legacy entries to the root of a configuration's schema and
    ///   to each of the root's properties, which is as deep as the old form
    ///   goes.
    /// </summary>
    /// <param name="wrapped">The generated schema, changed in place.</param>
    public static void Apply(WrappedJsonSchema wrapped)
    {
        if (wrapped.EmitContext is not { } context)
            return;

        var schema = wrapped.Schema;
        if (!wrapped.UiBuilders.TryGetValue(schema, out var classBuilder))
            return;

        // The old form lists the members by name in authored order, and draws nothing it is not told about.
        var propertyNames = classBuilder.Properties
            .GroupBy(x => x.MemberName, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.First().PropertyName, StringComparer.Ordinal);
        var structure = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in classBuilder.Structure)
        {
            if (entry.MemberType is not UiMemberKind.Property || !propertyNames.TryGetValue(entry.Name, out var propertyName))
                continue;

            if (schema.Properties.ContainsKey(propertyName))
                structure.TryAdd(propertyName, StructureProperty);
        }

        schema.ExtensionData ??= new Dictionary<string, object?>();
        schema.ExtensionData[ShokoJsonSchemaGenerator.UiDefinition] = new Dictionary<string, object?>
        {
            { SectionType, context.Convert(classBuilder.SectionType) },
            { Structure, structure },
        };

        foreach (var (propertyName, schemaValue) in schema.Properties)
        {
            if (classBuilder.GetProperty(ShokoJsonSchemaGenerator.GetPropertyKey(propertyName, schemaValue)) is not { } propertyBuilder)
                continue;

            // The validator's own bag, when the property has one, already names the element type.
            schemaValue.ExtensionData ??= new Dictionary<string, object?>();
            if (!schemaValue.ExtensionData.TryGetValue(ShokoJsonSchemaGenerator.UiDefinition, out var existing) ||
                existing is not Dictionary<string, object?> uiDefinition)
                schemaValue.ExtensionData[ShokoJsonSchemaGenerator.UiDefinition] = uiDefinition = [];

            // Named as a container, a nested class is left alone by the old form, which would otherwise edit it as text.
            var elementType = propertyBuilder.ElementType is DisplayElementType.Auto && schemaValue.ActualSchema.Properties.Count > 0
                ? DisplayElementType.SectionContainer
                : propertyBuilder.ElementType;
            uiDefinition[ShokoJsonSchemaGenerator.ElementType] = context.Convert(elementType);

            // The old form lower-cases the member's name, where the new contract writes "plain-text".
            if (propertyBuilder.Element is UiCodeEditorElementBuilder codeEditor)
                uiDefinition[CodeLanguage] = codeEditor.Language.ToString();
        }
    }
}
