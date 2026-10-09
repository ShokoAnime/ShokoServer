using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Namotion.Reflection;
using NJsonSchema;
using NJsonSchema.Generation;
using Shoko.Server.Utilities;

namespace Shoko.Server.Services.Configuration;

/// <summary>
///   A schema generator that describes a <c>[Flags]</c> enum as the list of
///   its members: an array of unique entries, each an enum of the single-bit
///   member names.
/// </summary>
/// <remarks>
///   Every use of a flags enum is a list, so the shared enum definition only
///   ever describes an entry. The other forms the converters accept on read
///   are never described. A default of a type parsable from text is written
///   as its text, the way the converters write it.
/// </remarks>
internal sealed class FlagEnumSchemaGenerator : JsonSchemaGenerator
{
    #region Fields

    /// <summary>
    ///   The arrays the generator built for a flags enum, with the enum and
    ///   the serialiser, so a validator can read the forms they replaced.
    /// </summary>
    private static readonly ConditionalWeakTable<JsonSchema, FlagEnumSchema> _flagEnumSchemas = [];

    private readonly FlagEnumReflectionService _reflectionService;

    private readonly bool _isNewtonsoftJson;

    #endregion

    #region Constructors

    /// <summary>
    ///   Creates a generator around the settings, wrapping their reflection
    ///   service.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <param name="isNewtonsoftJson">Whether Newtonsoft reads and writes the type.</param>
    public FlagEnumSchemaGenerator(JsonSchemaGeneratorSettings settings, bool isNewtonsoftJson)
        : base(Wrap(settings, isNewtonsoftJson))
    {
        _reflectionService = (FlagEnumReflectionService)settings.ReflectionService;
        _isNewtonsoftJson = isNewtonsoftJson;
    }

    private static JsonSchemaGeneratorSettings Wrap(JsonSchemaGeneratorSettings settings, bool isNewtonsoftJson)
    {
        settings.ReflectionService = new FlagEnumReflectionService(settings.ReflectionService, isNewtonsoftJson);
        return settings;
    }

    #endregion

    #region Lookup

    /// <summary>
    ///   Finds the flags enum an array schema was built for.
    /// </summary>
    /// <param name="schema">The schema, or the schema it references.</param>
    /// <param name="flagEnumSchema">The enum and serialiser, when found.</param>
    /// <returns><c>true</c> when the schema describes a flags enum.</returns>
    internal static bool TryGetFlagEnum(JsonSchema schema, out FlagEnumSchema flagEnumSchema)
        => _flagEnumSchemas.TryGetValue(schema, out flagEnumSchema!) || _flagEnumSchemas.TryGetValue(schema.ActualSchema, out flagEnumSchema!);

    #endregion

    #region Overrides

    /// <inheritdoc />
    protected override void GenerateArray<TSchemaType>(TSchemaType schema, JsonTypeDescription typeDescription, JsonSchemaResolver schemaResolver)
    {
        var contextualType = typeDescription.ContextualType;
        if (!FlagEnums.IsFlagEnum(contextualType.Type))
        {
            base.GenerateArray(schema, typeDescription, schemaResolver);
            return;
        }

        typeDescription.ApplyType(schema);
        schema.UniqueItems = true;
        _reflectionService.EntryDepth++;
        try
        {
            schema.Item = GenerateWithReference<JsonSchema>(contextualType, schemaResolver);
        }
        finally
        {
            _reflectionService.EntryDepth--;
        }

        _flagEnumSchemas.AddOrUpdate(schema, new(contextualType.Type, _isNewtonsoftJson));
    }

    /// <inheritdoc />
    public override object? ConvertDefaultValue(ContextualType type, object? defaultValue)
        => ConvertFlagEnum(defaultValue) ?? ConvertParsable(defaultValue) ?? base.ConvertDefaultValue(type, defaultValue);

    /// <inheritdoc />
    public override void ApplyDataAnnotations(JsonSchema schema, JsonTypeDescription typeDescription)
    {
        base.ApplyDataAnnotations(schema, typeDescription);

        // The wrapped service describes the member to itself as the enum it
        // is, and so hands a default over as the text the enum prints.
        var type = typeDescription.ContextualType.Type;
        if (schema.Default is string text && FlagEnums.IsFlagEnum(type))
            schema.Default = ConvertFlagEnum(FlagEnums.FromText(type, text, _isNewtonsoftJson));
        else if (ConvertFlagEnum(schema.Default) is { } names)
            schema.Default = names;
        else if (ConvertParsable(schema.Default) is { } parsableText)
            schema.Default = parsableText;
    }

    /// <summary>
    ///   The names a flags enum default is written as, or <c>null</c> for
    ///   any other value.
    /// </summary>
    private List<string>? ConvertFlagEnum(object? value)
        => value is Enum && FlagEnums.IsFlagEnum(value.GetType())
            ? FlagEnums.GetNames(value.GetType(), value, _isNewtonsoftJson).ToList()
            : null;

    /// <summary>
    ///   The text a default of a type parsable from text is written as, or
    ///   <c>null</c> for any other value.
    /// </summary>
    private string? ConvertParsable(object? value)
        => value is not null && ParsableTypes.IsConverted(value.GetType(), _isNewtonsoftJson, out _)
            ? ParsableTypes.ToText(value)
            : null;

    #endregion

    #region Nested Types

    /// <summary>
    ///   The flags enum an array schema describes, and whose names it uses.
    /// </summary>
    /// <param name="EnumType">The flags enum.</param>
    /// <param name="IsNewtonsoftJson">Whether the names are Newtonsoft's.</param>
    internal sealed record FlagEnumSchema(Type EnumType, bool IsNewtonsoftJson);

    /// <summary>
    ///   Describes a flags enum as an array, except while its entry is being
    ///   described, names each of its members alone rather than as the list
    ///   the serialiser writes, and defers everything else to the wrapped
    ///   service.
    /// </summary>
    private sealed class FlagEnumReflectionService(IReflectionService inner, bool isNewtonsoftJson) : IReflectionService
    {
        /// <summary>
        ///   How many entries are being described, during which a flags enum
        ///   is the enum it is.
        /// </summary>
        public int EntryDepth { get; set; }

        public Func<object, string?> GetEnumValueConverter(JsonSchemaGeneratorSettings settings)
        {
            var converter = inner.GetEnumValueConverter(settings);
            return value => value is Enum && FlagEnums.IsFlagEnum(value.GetType())
                ? FlagEnums.GetMemberName(value.GetType(), value, isNewtonsoftJson)
                : converter(value);
        }

        public string GetPropertyName(ContextualAccessorInfo accessorInfo, JsonSchemaGeneratorSettings settings)
            => inner.GetPropertyName(accessorInfo, settings);

        public void GenerateProperties(JsonSchema schema, ContextualType contextualType, JsonSchemaGeneratorSettings settings, JsonSchemaGenerator schemaGenerator, JsonSchemaResolver schemaResolver)
            => inner.GenerateProperties(schema, contextualType, settings, schemaGenerator, schemaResolver);

        public JsonTypeDescription GetDescription(ContextualType contextualType, ReferenceTypeNullHandling defaultReferenceTypeNullHandling, JsonSchemaGeneratorSettings settings)
            => Describe(contextualType, defaultReferenceTypeNullHandling, inner.GetDescription(contextualType, defaultReferenceTypeNullHandling, settings));

        public JsonTypeDescription GetDescription(ContextualType contextualType, JsonSchemaGeneratorSettings settings)
            => Describe(contextualType, settings.DefaultReferenceTypeNullHandling, inner.GetDescription(contextualType, settings));

        public bool IsNullable(ContextualType contextualType, ReferenceTypeNullHandling defaultReferenceTypeNullHandling)
            => inner.IsNullable(contextualType, defaultReferenceTypeNullHandling);

        public bool IsStringEnum(ContextualType contextualType, JsonSchemaGeneratorSettings settings)
            => inner.IsStringEnum(contextualType, settings);

        private JsonTypeDescription Describe(ContextualType contextualType, ReferenceTypeNullHandling defaultReferenceTypeNullHandling, JsonTypeDescription description)
            => EntryDepth is 0 && description.IsEnum && FlagEnums.IsFlagEnum(description.ContextualType.Type)
                ? JsonTypeDescription.Create(description.ContextualType, JsonObjectType.Array, inner.IsNullable(contextualType, defaultReferenceTypeNullHandling), null)
                : description;
    }

    #endregion
}
