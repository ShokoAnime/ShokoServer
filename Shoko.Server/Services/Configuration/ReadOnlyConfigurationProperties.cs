using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

using NewtonsoftSerializer = Newtonsoft.Json.JsonSerializer;

namespace Shoko.Server.Services.Configuration;

/// <summary>
///   Removes the properties a configuration cannot be given back from a
///   serialized configuration: those without a setter, such as a value
///   computed for display. They are written for the UI and the API, but never
///   read back, since both serializers replace rather than populate, so a
///   stored copy only goes stale.
/// </summary>
/// <remarks>
///   The document is walked next to the configuration's type, as the
///   serializer that wrote it sees the type, through objects, lists and
///   dictionaries. Anything the type does not declare, such as
///   <c>$schema</c>, is left alone.
/// </remarks>
internal sealed class ReadOnlyConfigurationProperties
{
    private const int MaxDepth = 64;

    private readonly IContractResolver _contractResolver;

    private readonly JsonSerializerOptions _systemTextJsonOptions;

    /// <summary>
    ///   Initializes a new instance of the
    ///   <see cref="ReadOnlyConfigurationProperties"/> class.
    /// </summary>
    /// <param name="newtonsoftSettings">The Newtonsoft settings configurations are written with.</param>
    /// <param name="systemTextJsonOptions">The System.Text.Json options configurations are written with.</param>
    public ReadOnlyConfigurationProperties(JsonSerializerSettings newtonsoftSettings, JsonSerializerOptions systemTextJsonOptions)
    {
        _contractResolver = NewtonsoftSerializer.Create(newtonsoftSettings).ContractResolver;
        _systemTextJsonOptions = new JsonSerializerOptions(systemTextJsonOptions)
        {
            TypeInfoResolver = systemTextJsonOptions.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver(),
        };
        _systemTextJsonOptions.MakeReadOnly();
    }

    /// <summary>
    ///   Removes the read-only properties of <paramref name="type"/> from
    ///   <paramref name="token"/>, in place.
    /// </summary>
    /// <param name="token">The serialized configuration.</param>
    /// <param name="type">The configuration's type.</param>
    /// <param name="useNewtonsoft">Whether the configuration is written with Newtonsoft.Json rather than System.Text.Json.</param>
    public void Strip(JToken token, Type type, bool useNewtonsoft)
    {
        if (useNewtonsoft)
            StripNewtonsoft(token, type, 0);
        else
            StripSystemTextJson(token, type, 0);
    }

    private void StripNewtonsoft(JToken? token, Type type, int depth)
    {
        if (token is null || depth > MaxDepth)
            return;

        switch (_contractResolver.ResolveContract(type))
        {
            case JsonObjectContract objectContract when token is JObject jObject:
                foreach (var property in objectContract.Properties)
                {
                    if (property.Ignored || property.PropertyName is not { } name || jObject.Property(name, StringComparison.Ordinal) is not { } jProperty)
                        continue;

                    if (!property.Writable)
                        jProperty.Remove();
                    else if (property.PropertyType is { } propertyType)
                        StripNewtonsoft(jProperty.Value, propertyType, depth + 1);
                }
                break;

            case JsonArrayContract { CollectionItemType: { } itemType } when token is JArray jArray:
                foreach (var item in jArray)
                    StripNewtonsoft(item, itemType, depth + 1);
                break;

            case JsonDictionaryContract { DictionaryValueType: { } valueType } when token is JObject jDictionary:
                foreach (var entry in jDictionary.Properties().ToList())
                    StripNewtonsoft(entry.Value, valueType, depth + 1);
                break;
        }
    }

    private void StripSystemTextJson(JToken? token, Type type, int depth)
    {
        if (token is null || depth > MaxDepth)
            return;

        var typeInfo = _systemTextJsonOptions.GetTypeInfo(type);
        switch (typeInfo.Kind)
        {
            case JsonTypeInfoKind.Object when token is JObject jObject:
                foreach (var property in typeInfo.Properties)
                {
                    if (jObject.Property(property.Name, StringComparison.Ordinal) is not { } jProperty)
                        continue;

                    if (property.Set is null)
                        jProperty.Remove();
                    else
                        StripSystemTextJson(jProperty.Value, property.PropertyType, depth + 1);
                }
                break;

            case JsonTypeInfoKind.Enumerable when token is JArray jArray && typeInfo.ElementType is { } itemType:
                foreach (var item in jArray)
                    StripSystemTextJson(item, itemType, depth + 1);
                break;

            case JsonTypeInfoKind.Dictionary when token is JObject jDictionary && typeInfo.ElementType is { } valueType:
                foreach (var entry in jDictionary.Properties().ToList())
                    StripSystemTextJson(entry.Value, valueType, depth + 1);
                break;
        }
    }
}
