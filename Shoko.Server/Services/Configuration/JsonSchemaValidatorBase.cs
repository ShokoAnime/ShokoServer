using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NJsonSchema;
using NJsonSchema.Validation;
using NJsonSchema.Validation.FormatValidators;

#nullable enable
namespace Shoko.Server.Services.Configuration;

/// <summary>
/// Base class for JSON schema validation because the included NJsonSchema
/// library's validator did not suit my needs around null-value validation,
/// among other things like incomplete path construction and no way to validate
/// "missing" properties in an extending class (ref
/// <see cref="ShokoJsonSchemaValidator{TConfig}"/>) nor extend existing logic
/// in an extending class if needed.
/// </summary>
/// Modified from: https://github.com/RicoSuter/NJsonSchema/blob/3066455159f71b72547a94f6651d1773733291e5/src/NJsonSchema/Validation/JsonSchemaValidator.cs
public class JsonSchemaValidatorBase
{
    private readonly Dictionary<string, IFormatValidator[]> _formatValidatorsMap;

    private readonly JsonSchemaValidatorSettings _settings;

    public JsonSchemaValidatorBase(JsonSchemaValidatorSettings? settings = null)
    {
        _settings = settings ?? new();
        _formatValidatorsMap = _settings.FormatValidators.GroupBy(x => x.Format).ToDictionary(v => v.Key, v => v.ToArray());
    }

    public (JToken Token, ICollection<ValidationError> Errors) Validate(string jsonData, JsonSchema schema, SchemaType schemaType = SchemaType.JsonSchema)
    {
        using var reader = new StringReader(jsonData);
        using var jsonReader = new JsonTextReader(reader) { DateParseHandling = DateParseHandling.None };
        var token = JToken.ReadFrom(jsonReader);
        return (token, Validate(null, token, schema.ActualSchema, schemaType, null, token.Path));
    }

    /// <summary>
    /// Unlike the original validator, this method is ran against _every_ property, with their proper path instead of being cut off.
    /// </summary>
    protected virtual ICollection<ValidationError> Validate(JToken? parentToken, JToken? token, JsonSchema schema, SchemaType schemaType, string? propertyName, string propertyPath)
    {
        var errors = new List<ValidationError>();
        if (token is null)
            return errors;

        ValidateAnyOf(parentToken, token, schema, schemaType, propertyName, propertyPath, errors);
        ValidateAllOf(parentToken, token, schema, schemaType, propertyName, propertyPath, errors);
        ValidateOneOf(parentToken, token, schema, schemaType, propertyName, propertyPath, errors);
        ValidateNot(parentToken, token, schema, schemaType, propertyName, propertyPath, errors);
        ValidateType(token, schema, schemaType, propertyName, propertyPath, errors);
        ValidateEnum(token, schema, schemaType, propertyName, propertyPath, errors);
        ValidateProperties(token, schema, schemaType, propertyName, propertyPath, errors);

        return errors;
    }

    protected virtual bool TryValidateChildSchema(JToken? parentToken, JToken token, JsonSchema schema, SchemaType schemaType, ValidationErrorKind errorKind, string property, string path, [NotNullWhen(false)] out ChildSchemaValidationError? error)
    {
        var errors = Validate(parentToken, token, schema.ActualSchema, schemaType, property, path);
        if (errors.Count == 0)
        {
            error = null;
            return true;
        }

        var errorDictionary = new Dictionary<JsonSchema, ICollection<ValidationError>> { { schema, errors } };
        error = new(errorKind, property, path, errorDictionary, token, schema);
        return false;
    }

    #region Any Of

    protected virtual void ValidateAnyOf(JToken? parentToken, JToken token, JsonSchema schema, SchemaType schemaType, string? propertyName, string propertyPath, List<ValidationError> errors)
    {
        if (schema.AnyOf.Count == 0)
            return;

        var propertyErrors = schema.AnyOf.ToDictionary(s => s, s => Validate(parentToken, token, s.ActualSchema, schemaType, propertyName, propertyPath));
        if (propertyErrors.All(s => s.Value.Count is not 0))
            errors.Add(new ChildSchemaValidationError(ValidationErrorKind.NotAnyOf, propertyName, propertyPath, propertyErrors, token, schema));
    }

    #endregion

    #region  All Of

    private void ValidateAllOf(JToken? parentToken, JToken token, JsonSchema schema, SchemaType schemaType, string? propertyName, string propertyPath, List<ValidationError> errors)
    {
        if (schema.AllOf.Count == 0)
            return;

        var propertyErrors = schema.AllOf.ToDictionary(s => s, s => Validate(parentToken, token, s.ActualSchema, schemaType, propertyName, propertyPath));
        if (propertyErrors.Any(s => s.Value.Count is not 0))
            errors.Add(new ChildSchemaValidationError(ValidationErrorKind.NotAllOf, propertyName, propertyPath, propertyErrors, token, schema));
    }

    #endregion

    #region One Of

    private void ValidateOneOf(JToken? parentToken, JToken token, JsonSchema schema, SchemaType schemaType, string? propertyName, string propertyPath, List<ValidationError> errors)
    {
        if (schema.OneOf.Count == 0)
            return;

        var propertyErrors = schema.OneOf.ToDictionary(s => s, s => Validate(parentToken, token, s.ActualSchema, schemaType, propertyName, propertyPath));
        if (propertyErrors.Count(s => s.Value.Count == 0) != 1)
            errors.Add(new ChildSchemaValidationError(ValidationErrorKind.NotOneOf, propertyName, propertyPath, propertyErrors, token, schema));
    }

    #endregion

    #region Not

    private void ValidateNot(JToken? parentToken, JToken token, JsonSchema schema, SchemaType schemaType, string? propertyName, string propertyPath, List<ValidationError> errors)
    {
        if (schema.Not is not null && Validate(parentToken, token, schema.Not, schemaType, propertyName, propertyPath).Count == 0)
            errors.Add(new ValidationError(ValidationErrorKind.ExcludedSchemaValidates, propertyName, propertyPath, token, schema));
    }

    #endregion

    #region Type

    private void ValidateType(JToken token, JsonSchema schema, SchemaType schemaType, string? propertyName, string propertyPath, List<ValidationError> errors)
    {
        if (token.Type == JTokenType.Null && schema.IsNullable(schemaType))
            return;

        var types = GetTypes(schema).ToDictionary(t => t, ICollection<ValidationError> (t) => []);
        if (types.Count > 1)
        {
            foreach (var type in types)
            {
                ValidateArray(token, schema, schemaType, type.Key, propertyName, propertyPath, (List<ValidationError>)type.Value);
                ValidateString(token, schema, type.Key, propertyName, propertyPath, (List<ValidationError>)type.Value);
                ValidateNumber(token, schema, type.Key, propertyName, propertyPath, (List<ValidationError>)type.Value);
                ValidateInteger(token, schema, type.Key, propertyName, propertyPath, (List<ValidationError>)type.Value);
                ValidateBoolean(token, schema, type.Key, propertyName, propertyPath, (List<ValidationError>)type.Value);
                ValidateNull(token, schema, type.Key, propertyName, propertyPath, (List<ValidationError>)type.Value);
                ValidateObject(token, schema, type.Key, propertyName, propertyPath, (List<ValidationError>)type.Value);
            }

            // just one has to validate when multiple types are defined
            if (types.All(t => t.Value.Count > 0))
                errors.Add(new MultiTypeValidationError(ValidationErrorKind.NoTypeValidates, propertyName, propertyPath, types, token, schema));
        }
        else
        {
            ValidateArray(token, schema, schemaType, schema.Type, propertyName, propertyPath, errors);
            ValidateString(token, schema, schema.Type, propertyName, propertyPath, errors);
            ValidateNumber(token, schema, schema.Type, propertyName, propertyPath, errors);
            ValidateInteger(token, schema, schema.Type, propertyName, propertyPath, errors);
            ValidateBoolean(token, schema, schema.Type, propertyName, propertyPath, errors);
            ValidateNull(token, schema, schema.Type, propertyName, propertyPath, errors);
            ValidateObject(token, schema, schema.Type, propertyName, propertyPath, errors);
        }
    }

    private static readonly IReadOnlySet<JsonObjectType> _jsonObjectTypes = Enum
        .GetValues(typeof(JsonObjectType))
        .Cast<JsonObjectType>()
        .Where(t => t != JsonObjectType.None)
        .ToHashSet();

    public static IEnumerable<JsonObjectType> GetTypes(JsonSchema schema)
        => _jsonObjectTypes.Where(t => schema.Type.HasFlag(t));

    #region Type | Array

    protected virtual void ValidateArray(JToken token, JsonSchema schema, SchemaType schemaType, JsonObjectType type, string? propertyName, string propertyPath, List<ValidationError> errors)
    {
        if (token is JArray array)
        {
            if (type is not JsonObjectType.Array)
                return;

            if (schema.MinItems > 0 && array.Count < schema.MinItems)
                errors.Add(new ValidationError(ValidationErrorKind.TooFewItems, propertyName, propertyPath, token, schema));

            if (schema.MaxItems > 0 && array.Count > schema.MaxItems)
                errors.Add(new ValidationError(ValidationErrorKind.TooManyItems, propertyName, propertyPath, token, schema));

            if (schema.UniqueItems && array.Count != array.Select(a => a.ToString()).Distinct().Count())
                errors.Add(new ValidationError(ValidationErrorKind.ItemsNotUnique, propertyName, propertyPath, token, schema));

            for (var index = 0; index < array.Count; index++)
            {
                var item = array[index];
                var propertyIndex = $"[{index}]";
                var itemPath = GetArrayPath(propertyPath, propertyIndex);
                if (schema.Item is not null)
                {
                    if (!TryValidateChildSchema(token, item, schema.Item, schemaType, ValidationErrorKind.ArrayItemNotValid, propertyIndex, itemPath, out var error))
                        errors.Add(error);
                }
                else if (schema.Items.Count > 0)
                {
                    if (schema.Items.Count > index)
                    {
                        if (!TryValidateChildSchema(token, item, schema.Items.ElementAt(index), schemaType, ValidationErrorKind.ArrayItemNotValid, propertyIndex, GetArrayPath(propertyPath, propertyIndex), out var error))
                            errors.Add(error);
                    }
                    else if (schema.AdditionalItemsSchema is not null)
                    {
                        if (!TryValidateChildSchema(token, item, schema.AdditionalItemsSchema, schemaType, ValidationErrorKind.AdditionalItemNotValid, propertyIndex, GetArrayPath(propertyPath, propertyIndex), out var error))
                            errors.Add(error);
                    }
                    else if (!schema.AllowAdditionalItems)
                    {
                        errors.Add(new ValidationError(ValidationErrorKind.TooManyItemsInTuple, propertyIndex, GetArrayPath(propertyPath, propertyIndex), item, schema));
                    }
                }
            }
        }
        else if (type is JsonObjectType.Array)
        {
            errors.Add(new ValidationError(ValidationErrorKind.ArrayExpected, propertyName, propertyPath, token, schema));
        }
    }

    #region Type | Array | Get Path

    protected virtual string GetArrayPath(string propertyPath, string propertyIndex)
        => !string.IsNullOrEmpty(propertyPath) ? propertyPath + propertyIndex : propertyIndex;

    #endregion

    #endregion

    #region Type | String

    protected virtual void ValidateString(JToken token, JsonSchema schema, JsonObjectType type, string? propertyName, string propertyPath, List<ValidationError> errors)
    {
        var isString = token.Type is JTokenType.String or JTokenType.Date or JTokenType.Guid or JTokenType.TimeSpan or JTokenType.Uri;
        if (isString)
        {
            if (type is not JsonObjectType.String)
                return;

            var value = token.Type == JTokenType.Date && token is JValue jValue
                ? jValue.ToString("yyyy-MM-ddTHH:mm:ssK", CultureInfo.InvariantCulture)
                : token.Value<string>();

            if (value is not null)
            {
                if (!string.IsNullOrEmpty(schema.Pattern) && !Regex.IsMatch(value, schema.Pattern))
                    errors.Add(new ValidationError(ValidationErrorKind.PatternMismatch, propertyName, propertyPath, token, schema));

                if (schema.MinLength.HasValue && value.Length < schema.MinLength)
                    errors.Add(new ValidationError(ValidationErrorKind.StringTooShort, propertyName, propertyPath, token, schema));

                if (schema.MaxLength.HasValue && value.Length > schema.MaxLength)
                    errors.Add(new ValidationError(ValidationErrorKind.StringTooLong, propertyName, propertyPath, token, schema));

                if (
                    !string.IsNullOrEmpty(schema.Format) &&
                    _formatValidatorsMap.TryGetValue(schema.Format!, out var formatValidators) &&
                    !formatValidators.Any(x => x.IsValid(value, token.Type))
                )
                    errors.AddRange(
                        formatValidators
                            .Select(x => x.ValidationErrorKind)
                            .Distinct()
                            .Select(validationErrorKind => new ValidationError(validationErrorKind, propertyName, propertyPath, token, schema))
                    );
            }
        }
        else if (type is JsonObjectType.String)
        {
            errors.Add(new ValidationError(ValidationErrorKind.StringExpected, propertyName, propertyPath, token, schema));
        }
    }

    #endregion

    #region Type | Number

    protected virtual void ValidateNumber(JToken token, JsonSchema schema, JsonObjectType type, string? propertyName, string propertyPath, List<ValidationError> errors)
    {
        if (type is JsonObjectType.Number && token.Type is not JTokenType.Float and not JTokenType.Integer)
            errors.Add(new ValidationError(ValidationErrorKind.NumberExpected, propertyName, propertyPath, token, schema));

        if (token.Type is JTokenType.Float or JTokenType.Integer)
        {
            if (type is not JsonObjectType.Number and not JsonObjectType.Integer)
                return;

            try
            {
                var value = token.Value<decimal>();
                if (schema.Minimum.HasValue && (schema.IsExclusiveMinimum ? value <= schema.Minimum : value < schema.Minimum))
                    errors.Add(new ValidationError(ValidationErrorKind.NumberTooSmall, propertyName, propertyPath, token, schema));

                if (schema.Maximum.HasValue && (schema.IsExclusiveMaximum ? value >= schema.Maximum : value > schema.Maximum))
                    errors.Add(new ValidationError(ValidationErrorKind.NumberTooBig, propertyName, propertyPath, token, schema));

                if (schema.ExclusiveMinimum.HasValue && value <= schema.ExclusiveMinimum)
                    errors.Add(new ValidationError(ValidationErrorKind.NumberTooSmall, propertyName, propertyPath, token, schema));

                if (schema.ExclusiveMaximum.HasValue && value >= schema.ExclusiveMaximum)
                    errors.Add(new ValidationError(ValidationErrorKind.NumberTooBig, propertyName, propertyPath, token, schema));

                if (schema.MultipleOf.HasValue && value % schema.MultipleOf is not 0)
                    errors.Add(new ValidationError(ValidationErrorKind.NumberNotMultipleOf, propertyName, propertyPath, token, schema));
            }
            catch (OverflowException)
            {
                var value = token.Value<double>();

                if (schema.Minimum.HasValue && (schema.IsExclusiveMinimum ? value <= (double)schema.Minimum : value < (double)schema.Minimum))
                    errors.Add(new ValidationError(ValidationErrorKind.NumberTooSmall, propertyName, propertyPath, token, schema));

                if (schema.Maximum.HasValue && (schema.IsExclusiveMaximum ? value >= (double)schema.Maximum : value > (double)schema.Maximum))
                    errors.Add(new ValidationError(ValidationErrorKind.NumberTooBig, propertyName, propertyPath, token, schema));

                if (schema.ExclusiveMinimum.HasValue && value <= (double)schema.ExclusiveMinimum)
                    errors.Add(new ValidationError(ValidationErrorKind.NumberTooSmall, propertyName, propertyPath, token, schema));

                if (schema.ExclusiveMaximum.HasValue && value >= (double)schema.ExclusiveMaximum)
                    errors.Add(new ValidationError(ValidationErrorKind.NumberTooBig, propertyName, propertyPath, token, schema));

                if (schema.MultipleOf.HasValue && value % (double)schema.MultipleOf is not 0)
                    errors.Add(new ValidationError(ValidationErrorKind.NumberNotMultipleOf, propertyName, propertyPath, token, schema));
            }
        }
    }

    #endregion

    #region Type | Integer

    protected virtual void ValidateInteger(JToken token, JsonSchema schema, JsonObjectType type, string? propertyName, string propertyPath, List<ValidationError> errors)
    {
        if (type is JsonObjectType.Integer && token.Type is not JTokenType.Integer)
            errors.Add(new ValidationError(ValidationErrorKind.IntegerExpected, propertyName, propertyPath, token, schema));
    }

    #endregion

    #region Type | Boolean

    protected virtual void ValidateBoolean(JToken token, JsonSchema schema, JsonObjectType type, string? propertyName, string propertyPath, List<ValidationError> errors)
    {
        if (type is JsonObjectType.Boolean && token.Type is not JTokenType.Boolean)
            errors.Add(new ValidationError(ValidationErrorKind.BooleanExpected, propertyName, propertyPath, token, schema));
    }

    #endregion

    #region Type | Null

    protected virtual void ValidateNull(JToken token, JsonSchema schema, JsonObjectType type, string? propertyName, string propertyPath, List<ValidationError> errors)
    {
        if (type is JsonObjectType.Null && token is not null && token.Type is not JTokenType.Null)
            errors.Add(new ValidationError(ValidationErrorKind.NullExpected, propertyName, propertyPath, token, schema));
    }

    #endregion

    #region Type | Object

    protected virtual void ValidateObject(JToken token, JsonSchema schema, JsonObjectType type, string? propertyName, string propertyPath, List<ValidationError> errors)
    {
        if (type is JsonObjectType.Object && token is not JObject)
            errors.Add(new ValidationError(ValidationErrorKind.ObjectExpected, propertyName, propertyPath, token, schema));
    }

    #endregion

    #endregion

    #region Enum

    protected virtual void ValidateEnum(JToken token, JsonSchema schema, SchemaType schemaType, string? propertyName, string propertyPath, List<ValidationError> errors)
    {
        if (schema.IsNullable(schemaType) && token.Type is JTokenType.Null)
            return;

        if (schema.Enumeration.Count > 0 && schema.Enumeration.All(v => v?.ToString() != token?.ToString()))
            errors.Add(new ValidationError(ValidationErrorKind.NotInEnumeration, propertyName, propertyPath, token, schema));
    }

    #endregion

    #region Properties

    protected virtual void ValidateProperties(JToken token, JsonSchema schema, SchemaType schemaType, string? propertyName, string propertyPath, List<ValidationError> errors)
    {
        var obj = token as JObject;
        if (obj is null && schema.Type.HasFlag(JsonObjectType.Null))
            return;

        var schemaPropertyKeys = new HashSet<string>(schema.Properties.Keys, _settings.PropertyStringComparer);
        foreach (var propertyInfo in schema.Properties)
        {
            var subPropertyPath = GetPropertyPath(propertyPath, propertyInfo.Key);
            TryGetPropertyWithStringComparer(obj, propertyInfo.Key, out var value);
            var propertyErrors = Validate(obj, value, propertyInfo.Value.ActualSchema, schemaType, propertyInfo.Key, subPropertyPath);
            errors.AddRange(propertyErrors);
        }

        // Properties may be required in a schema without being specified as a property.
        foreach (var requiredProperty in schema.RequiredProperties)
        {
            if (obj is null || !TryGetPropertyWithStringComparer(obj, requiredProperty, out _))
            {
                var subPropertyPath = GetPropertyPath(propertyPath, requiredProperty);
                errors.Add(new ValidationError(ValidationErrorKind.PropertyRequired, requiredProperty, subPropertyPath, token, schema));
            }
        }

        if (obj is not null)
        {
            var properties = obj.Properties().ToList();
            ValidateMaxProperties(token, properties, schema, propertyName, propertyPath, errors);
            ValidateMinProperties(token, properties, schema, propertyName, propertyPath, errors);

            var additionalProperties = properties.Where(p => !schemaPropertyKeys.Contains(p.Name)).ToList();
            ValidatePatternProperties(obj, additionalProperties, schema, schemaType, propertyPath, errors);
            ValidateAdditionalProperties(obj, additionalProperties, schema, schemaType, propertyPath, errors);
        }
    }

    #region Properties | Max

    protected virtual void ValidateMaxProperties(JToken token, List<JProperty> properties, JsonSchema schema, string? propertyName, string propertyPath, List<ValidationError> errors)
    {
        if (schema.MaxProperties > 0 && properties.Count > schema.MaxProperties)
            errors.Add(new ValidationError(ValidationErrorKind.TooManyProperties, propertyName, propertyPath, token, schema));
    }

    #endregion

    #region Properties | Min

    protected virtual void ValidateMinProperties(JToken token, List<JProperty> properties, JsonSchema schema, string? propertyName, string propertyPath, List<ValidationError> errors)
    {
        if (schema.MinProperties > 0 && properties.Count < schema.MinProperties)
            errors.Add(new ValidationError(ValidationErrorKind.TooFewProperties, propertyName, propertyPath, token, schema));
    }

    #endregion

    #region Properties | Pattern

    protected virtual void ValidatePatternProperties(JToken? parentToken, List<JProperty> additionalProperties, JsonSchema schema, SchemaType schemaType, string propertyPath, List<ValidationError> errors)
    {
        foreach (var property in additionalProperties.ToArray())
        {
            var subPropertyPath = GetPropertyPath(propertyPath, property.Name);
            var patternPropertySchema = schema.PatternProperties.FirstOrDefault(p => Regex.IsMatch(property.Name, p.Key));
            if (patternPropertySchema.Value is not null)
            {
                if (!TryValidateChildSchema(parentToken, property.Value, patternPropertySchema.Value, schemaType, ValidationErrorKind.AdditionalPropertiesNotValid, property.Name, subPropertyPath, out var error))
                    errors.Add(error);

                additionalProperties.Remove(property);
            }
        }
    }

    #endregion

    #region Properties | Additional

    protected virtual void ValidateAdditionalProperties(JToken? parentToken, List<JProperty> additionalProperties, JsonSchema schema, SchemaType schemaType, string propertyPath, List<ValidationError> errors)
    {
        if (schema.AdditionalPropertiesSchema is not null)
        {
            foreach (var property in additionalProperties)
            {
                var subPropertyPath = GetPropertyPath(propertyPath, property.Name, alwaysEscape: true);
                if (!TryValidateChildSchema(parentToken, property.Value, schema.AdditionalPropertiesSchema, schemaType, ValidationErrorKind.AdditionalPropertiesNotValid, property.Name, subPropertyPath, out var error))
                    errors.Add(error);
            }
        }
        else if (!schema.AllowAdditionalProperties && additionalProperties.Count > 0)
        {
            foreach (var property in additionalProperties)
            {
                var subPropertyPath = GetPropertyPath(propertyPath, property.Name, alwaysEscape: true);
                errors.Add(new ValidationError(ValidationErrorKind.NoAdditionalPropertiesAllowed, property.Name, subPropertyPath, property, schema));
            }
        }
    }

    #endregion

    #region Properties | Get Path

    private static readonly HashSet<char> _invalidPropertyNameCharacters = ['.', '[', ']', '(', ')', '{', '}', '"', '/', '\\', '\'', ' ', '\t', '\r', '\n', '\b', '\f'];

    protected virtual string GetPropertyPath(string propertyPath, string propertyName, bool alwaysEscape = false)
    {
        // Escape property name if it contains any of these characters.
        if (alwaysEscape || propertyName.Any(_invalidPropertyNameCharacters.Contains))
        {
            if (propertyName.Contains('\''))
                propertyName = propertyName.Replace("'", "\\'");

            propertyName = $"['{propertyName}']";
            return !string.IsNullOrEmpty(propertyPath) ? propertyPath + propertyName : propertyName;
        }

        return !string.IsNullOrEmpty(propertyPath) ? propertyPath + "." + propertyName : propertyName;
    }

    #endregion

    #region Properties | Try Get Prop.

    // This method mimics the behavior of the JObject.TryGetValue(string property, StringComparison comparison, out JToken)
    // extension method using a StringComparer class instead of StringComparison enum value.
    protected bool TryGetPropertyWithStringComparer(JObject? obj, string propertyName, [NotNullWhen(true)] out JToken? value)
    {
        if (obj is null)
        {
            value = null;
            return false;
        }

        if (obj.TryGetValue(propertyName, out value))
            return true;

        foreach (var property in obj.Properties())
        {
            if (_settings.PropertyStringComparer.Equals(propertyName, property.Name))
            {
                value = property.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    #endregion

    #endregion
}
