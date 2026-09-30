using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Shoko.Abstractions.Metadata;
using Shoko.Server.API.Converters;

namespace Shoko.Server.API.Resolvers;

/// <summary>
///   The contract resolver for the API's JSON. It writes every
///   <see cref="MetadataSource"/> and <see cref="MetadataEntityType"/>, as a
///   value or a dictionary key, as the old enums spelled it (see
///   <see cref="LegacyMetadataSpellings"/>), and reads the value, an alias or
///   an old spelling of registered ones only, except in the resolver for
///   <see cref="ForSettings">settings</see>.
/// </summary>
public class ApiContractResolver : DefaultContractResolver
{
    private readonly bool _registeredOnly;

    /// <summary>
    ///   Creates a resolver that keeps member names as they are and reads
    ///   registered sources and entity types only.
    /// </summary>
    public ApiContractResolver() : this(registeredOnly: true) { }

    private ApiContractResolver(bool registeredOnly)
    {
        _registeredOnly = registeredOnly;
        NamingStrategy = new DefaultNamingStrategy();
    }

    /// <summary>
    ///   The resolver for patching the server settings. It writes as the API
    ///   does, but reads any valid source or entity type, as the settings file
    ///   does, so one whose plugin isn't loaded can be sent back as it was
    ///   received.
    /// </summary>
    public static ApiContractResolver ForSettings { get; } = new(registeredOnly: false);

    /// <summary>
    ///   Picks the converter for a type: the value converter for a
    ///   <see cref="MetadataSource"/> or <see cref="MetadataEntityType"/>, and
    ///   a reader that takes registered keys only for a dictionary keyed by
    ///   one.
    /// </summary>
    /// <param name="objectType">The type.</param>
    /// <returns>The converter, or <c>null</c> if the type has none.</returns>
    protected override JsonConverter? ResolveContractConverter(Type objectType)
    {
        if (objectType == typeof(MetadataSource))
            return _registeredOnly ? MetadataSourceValueConverter.Instance : MetadataSourceValueConverter.Lenient;
        if (objectType == typeof(MetadataEntityType))
            return _registeredOnly ? MetadataEntityTypeValueConverter.Instance : MetadataEntityTypeValueConverter.Lenient;
        if (_registeredOnly && MetadataSourceDictionaryConverter.TryGetValueType(objectType, out _))
            return MetadataSourceDictionaryConverter.Instance;
        if (_registeredOnly && MetadataEntityTypeDictionaryConverter.TryGetValueType(objectType, out _))
            return MetadataEntityTypeDictionaryConverter.Instance;
        return base.ResolveContractConverter(objectType);
    }

    /// <summary>
    ///   Makes the contract for a dictionary, writing the keys of one keyed by
    ///   a <see cref="MetadataSource"/> or <see cref="MetadataEntityType"/> as
    ///   the old enums spelled them. The key reaches the resolver as its value.
    /// </summary>
    /// <param name="objectType">The dictionary type.</param>
    /// <returns>The contract.</returns>
    protected override JsonDictionaryContract CreateDictionaryContract(Type objectType)
    {
        var contract = base.CreateDictionaryContract(objectType);
        if (contract.DictionaryKeyType == typeof(MetadataSource))
            contract.DictionaryKeyResolver = key => LegacyMetadataSpellings.OfSourceValue(key);
        else if (contract.DictionaryKeyType == typeof(MetadataEntityType))
            contract.DictionaryKeyResolver = key => LegacyMetadataSpellings.OfEntityTypeValue(key);
        return contract;
    }
}
