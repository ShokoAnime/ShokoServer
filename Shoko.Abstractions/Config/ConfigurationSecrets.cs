using System;
using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

using NewtonsoftJsonIgnoreAttribute = Newtonsoft.Json.JsonIgnoreAttribute;
using NewtonsoftJsonPropertyAttribute = Newtonsoft.Json.JsonPropertyAttribute;
using SystemTextJsonIgnoreAttribute = System.Text.Json.Serialization.JsonIgnoreAttribute;
using SystemTextJsonPropertyNameAttribute = System.Text.Json.Serialization.JsonPropertyNameAttribute;

namespace Shoko.Abstractions.Config;

/// <summary>
///   Masks and restores secret values in a serialized configuration document.
/// </summary>
/// <remarks>
///   <para>
///     A configuration model always holds the truth. Anything that reads a
///     configuration through the object graph — a plugin calling
///     <c>Load&lt;TConfig&gt;()</c>, a service reading its own settings — keeps
///     seeing the real credentials. Masking belongs to the transport: it is
///     applied when a configuration is serialized for something outside the
///     server, and undone when a document comes back in.
///   </para>
///   <para>
///     A property is a secret when it is marked with
///     <see cref="PasswordPropertyTextAttribute"/> or with
///     <see cref="DataTypeAttribute"/> naming
///     <see cref="DataType.Password"/>, the same pair the schema generator
///     renders a password field for. Masking has to follow whatever renders as
///     a password, or a field the user is shown stars for goes out in
///     plaintext.
///   </para>
///   <para>
///     On the way out, a secret that holds a value is replaced with a masked
///     value: <see cref="Sentinel"/> with a fingerprint of the secret folded
///     into it, <c>***SECRET-UNCHANGED:&lt;fingerprint&gt;***</c>. The
///     fingerprint is a keyed hash under a key that belongs to the install and
///     never leaves it, so it tells a client nothing about the value beyond
///     whether two secrets are equal. A secret that holds <c>null</c> or an
///     empty string is left alone, so "nothing configured" stays
///     distinguishable from "withheld".
///   </para>
///   <para>
///     On the way back in, <see cref="Restore"/> merges the incoming document
///     against the currently stored one. This is deliberately not a
///     <c>JsonConverter</c>: a converter sees only the incoming value and cannot
///     reach the stored one, so it could never put the real secret back. The
///     four cases are:
///   </para>
///   <list type="bullet">
///     <item><description>the property is absent — a patch that does not mention it; the stored value is kept;</description></item>
///     <item><description>the property holds a masked value — it round-tripped unchanged; the stored value is kept;</description></item>
///     <item><description>the property holds some other value — the user changed it; the incoming value wins;</description></item>
///     <item><description>the property is explicitly <c>null</c> or an empty string — the user cleared it; the secret is cleared.</description></item>
///   </list>
///   <para>
///     The last case is why simply skipping the sentinel on deserialize would
///     not do: clearing a secret has to stay possible, and has to stay
///     distinguishable from leaving it alone.
///   </para>
///   <para>
///     Restoring a secret nested inside a list is what the fingerprint is for.
///     The position in a list cannot pair an incoming element with a stored
///     one, since a reorder, an insert or a delete moves every secret after it
///     onto the wrong element, and nothing else on an element is guaranteed to
///     identify it. So inside a list a masked value is matched by its
///     fingerprint against every stored secret at the same property, whichever
///     element it sits on. Two elements holding the same secret share a
///     fingerprint, which is harmless, since either match restores the same
///     value. A masked value whose fingerprint matches nothing stored, or the
///     bare <see cref="Sentinel"/> inside a list, is rejected rather than
///     guessed at. A dictionary pairs its entries up by key, as does every
///     object outside a list, so the fingerprint is not needed there.
///   </para>
/// </remarks>
public static class ConfigurationSecrets
{
    #region Constants

    /// <summary>
    ///   The bare form of a masked secret. Masking always adds a fingerprint,
    ///   <c>***SECRET-UNCHANGED:&lt;fingerprint&gt;***</c>, but this form is
    ///   still accepted anywhere outside a list, where the stored value can be
    ///   found without one. Use <see cref="IsMasked(string?)"/> to recognise
    ///   either form.
    /// </summary>
    /// <remarks>
    ///   This is deliberately not the <c>***HIDDEN***</c> marker used when
    ///   dumping settings to the log. The log marker is one-way and means
    ///   nothing more than "not shown here", while this one carries round-trip
    ///   meaning: sending it back asks the server to keep what it already has.
    ///   Sharing a single constant between the two would invite treating them as
    ///   interchangeable.
    /// </remarks>
    public const string Sentinel = "***SECRET-UNCHANGED***";

    private const string MaskedPrefix = "***SECRET-UNCHANGED:";

    private const string MaskedSuffix = "***";

    /// <summary>
    ///   The number of hash bytes kept for a fingerprint. Sixteen bytes is far
    ///   beyond what an accidental collision between two stored secrets needs.
    /// </summary>
    private const int FingerprintLength = 16;

    #endregion

    #region Caches

    private static readonly ConcurrentDictionary<Type, IReadOnlyDictionary<string, PropertyInfo>> _serializableProperties = [];

    private static readonly ConcurrentDictionary<Type, bool> _containsSecrets = [];

    #endregion

    #region Public API

    /// <summary>
    ///   Checks whether <paramref name="type"/> has a secret anywhere in its
    ///   serialized shape, directly or through a nested object, list or
    ///   dictionary.
    /// </summary>
    /// <param name="type">
    ///   The configuration type to inspect.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when <paramref name="type"/> is <c>null</c>.
    /// </exception>
    /// <returns>
    ///   <c>true</c> when the type carries at least one secret.
    /// </returns>
    public static bool ContainsSecrets(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return ContainsSecrets(type, []);
    }

    /// <summary>
    ///   Resolves a secret an endpoint was handed against the one that is
    ///   stored, for an endpoint that uses a secret rather than saving it.
    /// </summary>
    /// <remarks>
    ///   A client reads a configuration with its secrets masked, so a form that
    ///   posts a secret straight back, to test a login with, sends the mask
    ///   unless the user retyped it. The mask says "the stored one", which is
    ///   what it resolves to here. Saving goes through the restore path
    ///   instead, which pairs each mask with the value it stands for.
    /// </remarks>
    /// <param name="posted">What the caller sent.</param>
    /// <param name="stored">What is stored for that property.</param>
    /// <returns>The secret to use.</returns>
    public static string? Resolve(string? posted, string? stored)
        => IsMasked(posted) ? stored : posted;

    /// <summary>
    ///   Checks whether <paramref name="value"/> is a masked secret, in either
    ///   the bare or the fingerprinted form.
    /// </summary>
    /// <param name="value">
    ///   The value to check.
    /// </param>
    /// <returns>
    ///   <c>true</c> when the value stands for a withheld secret.
    /// </returns>
    public static bool IsMasked([NotNullWhen(true)] string? value)
        => value is not null && (string.Equals(value, Sentinel, StringComparison.Ordinal) || TryGetFingerprint(value, out _));

    /// <summary>
    ///   Replaces every set secret in <paramref name="token"/> with a masked
    ///   value carrying its fingerprint.
    /// </summary>
    /// <param name="token">
    ///   The serialized configuration. It is not modified; a masked copy is
    ///   returned.
    /// </param>
    /// <param name="type">
    ///   The configuration type the document was serialized from.
    /// </param>
    /// <param name="fingerprintKey">
    ///   The key the fingerprints are computed under. It has to be the same key
    ///   <see cref="Restore"/> is later given, and it must never be sent to
    ///   whoever receives the masked document.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when <paramref name="token"/>, <paramref name="type"/> or
    ///   <paramref name="fingerprintKey"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   Thrown when <paramref name="fingerprintKey"/> is empty.
    /// </exception>
    /// <returns>
    ///   A masked copy of the document.
    /// </returns>
    public static JToken Mask(JToken token, Type type, byte[] fingerprintKey)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(type);
        ValidateKey(fingerprintKey);

        var masked = token.DeepClone();
        MaskValue(masked, type, fingerprintKey);
        return masked;
    }

    /// <summary>
    ///   Puts the stored secrets back into an incoming document, so that a
    ///   masked document can be saved without destroying what it was masking.
    /// </summary>
    /// <param name="incoming">
    ///   The incoming document. It is not modified; a restored copy is returned.
    /// </param>
    /// <param name="current">
    ///   The currently stored document, or <c>null</c> when nothing has been
    ///   persisted yet. With nothing stored there is nothing to restore, and an
    ///   incoming masked value is reported as an error instead.
    /// </param>
    /// <param name="type">
    ///   The configuration type both documents belong to.
    /// </param>
    /// <param name="fingerprintKey">
    ///   The key the incoming document was masked under.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when <paramref name="incoming"/>, <paramref name="type"/> or
    ///   <paramref name="fingerprintKey"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   Thrown when <paramref name="fingerprintKey"/> is empty.
    /// </exception>
    /// <returns>
    ///   The restored copy, and any errors keyed by the property path that
    ///   produced them. A non-empty error set means the document must be
    ///   rejected rather than saved.
    /// </returns>
    public static (JToken Token, IReadOnlyDictionary<string, IReadOnlyList<string>> Errors) Restore(JToken incoming, JToken? current, Type type, byte[] fingerprintKey)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        ArgumentNullException.ThrowIfNull(type);
        ValidateKey(fingerprintKey);

        var restored = incoming.DeepClone();
        var errors = new Dictionary<string, List<string>>();
        RestoreValue(restored, current, type, string.Empty, string.Empty, errors, fingerprintKey, pool: null);
        return (restored, errors.ToDictionary(a => a.Key, a => (IReadOnlyList<string>)a.Value));
    }

    #endregion

    #region Masking

    private static void MaskValue(JToken token, Type declaredType, byte[] key)
    {
        if (GetDictionaryValueType(declaredType) is { } dictionaryValueType)
        {
            if (token is JObject map)
                foreach (var entry in map.Properties())
                    MaskValue(entry.Value, dictionaryValueType, key);
            return;
        }

        if (GetEnumerableElementType(declaredType) is { } elementType)
        {
            if (token is JArray array)
                foreach (var item in array)
                    MaskValue(item, elementType, key);
            return;
        }

        if (token is JObject obj && IsWalkable(declaredType))
            MaskObject(obj, declaredType, key);
    }

    private static void MaskObject(JObject obj, Type type, byte[] key)
    {
        var properties = GetSerializableProperties(type);
        foreach (var jsonProperty in obj.Properties())
        {
            if (!properties.TryGetValue(jsonProperty.Name, out var property))
                continue;

            if (IsSecret(property))
            {
                if (jsonProperty.Value.Type is JTokenType.String && jsonProperty.Value.Value<string>() is { Length: > 0 } secret)
                    jsonProperty.Value = $"{MaskedPrefix}{GetFingerprint(secret, key)}{MaskedSuffix}";
                continue;
            }

            MaskValue(jsonProperty.Value, property.PropertyType, key);
        }
    }

    #endregion

    #region Restoring

    // `path` is the concrete path, with list indices and dictionary keys, used
    // to report errors. `shape` is the path through the type's properties
    // alone, which scopes a fingerprint match to one property. `pool` is set
    // once the walk is inside a list, and holds every stored secret under that
    // list keyed by shape and fingerprint.
    private static void RestoreValue(JToken incoming, JToken? current, Type declaredType, string path, string shape, Dictionary<string, List<string>> errors, byte[] key, Dictionary<string, string>? pool)
    {
        if (!ContainsSecrets(declaredType))
            return;

        if (GetDictionaryValueType(declaredType) is { } dictionaryValueType)
        {
            if (incoming is not JObject map)
                return;

            // A dictionary key is an identity by construction, so outside a list
            // the entries pair up by it.
            var currentMap = current as JObject;
            foreach (var entry in map.Properties())
                RestoreValue(entry.Value, currentMap?.Property(entry.Name, StringComparison.Ordinal)?.Value, dictionaryValueType, Join(path, entry.Name), shape, errors, key, pool);
            return;
        }

        if (GetEnumerableElementType(declaredType) is { } elementType)
        {
            if (incoming is not JArray array)
                return;

            // Nothing about a list element's position or content identifies it, so
            // from here on a masked value is found by its fingerprint among every
            // secret stored under this list. A nested list is already covered by
            // the outermost one's pool.
            if (pool is null)
            {
                pool = [];
                if (current is JArray currentArray)
                    foreach (var element in currentArray)
                        CollectSecrets(element, elementType, shape, key, pool);
            }

            for (var index = 0; index < array.Count; index++)
                RestoreValue(array[index], null, elementType, $"{path}[{index}]", shape, errors, key, pool);
            return;
        }

        if (incoming is JObject obj && IsWalkable(declaredType))
            RestoreObject(obj, current as JObject, declaredType, path, shape, errors, key, pool);
    }

    private static void RestoreObject(JObject incoming, JObject? current, Type type, string path, string shape, Dictionary<string, List<string>> errors, byte[] key, Dictionary<string, string>? pool)
    {
        foreach (var (jsonName, property) in GetSerializableProperties(type))
        {
            var incomingProperty = incoming.Property(jsonName, StringComparison.OrdinalIgnoreCase);
            var currentValue = current?.Property(jsonName, StringComparison.OrdinalIgnoreCase)?.Value;
            var propertyPath = Join(path, property.Name);
            var propertyShape = Join(shape, property.Name);
            if (IsSecret(property))
            {
                RestoreSecret(incoming, incomingProperty, jsonName, currentValue, propertyPath, propertyShape, errors, pool);
                continue;
            }

            if (incomingProperty is not null)
                RestoreValue(incomingProperty.Value, currentValue, property.PropertyType, propertyPath, propertyShape, errors, key, pool);
        }
    }

    private static void RestoreSecret(JObject incoming, JProperty? incomingProperty, string jsonName, JToken? currentValue, string path, string shape, Dictionary<string, List<string>> errors, Dictionary<string, string>? pool)
    {
        var stored = currentValue is { Type: JTokenType.String } ? currentValue.Value<string>() : null;
        var hasStored = !string.IsNullOrEmpty(stored);

        // Absent: the sender never mentioned the property, so this is a patch and
        // the stored value has to survive it.
        if (incomingProperty is null)
        {
            if (hasStored)
                incoming.Add(jsonName, stored);
            return;
        }

        // Anything that is not masked is the sender's own intent: an explicit
        // null or empty string clears the secret, any other value replaces it.
        // Both stand as sent.
        var value = incomingProperty.Value.Type is JTokenType.String ? incomingProperty.Value.Value<string>() : null;
        if (!IsMasked(value))
            return;

        // Inside a list the fingerprint is the only thing that says which stored
        // secret this was, so a bare sentinel, or a fingerprint nothing stored
        // matches, is refused rather than guessed at.
        if (pool is not null)
        {
            if (TryGetFingerprint(value, out var fingerprint) && pool.TryGetValue(PoolKey(shape, fingerprint), out var match))
            {
                incomingProperty.Value = match;
                return;
            }

            AddError(errors, path, fingerprint is null
                ? $"\"{Sentinel}\" cannot be restored inside a list, because it does not say which stored value it stands for. Send the masked value you were given, or the real value."
                : "The masked value does not match any value stored for this property, so it cannot be restored. Send the real value instead.");
            return;
        }

        // Outside a list there is only one stored value it can stand for.
        if (hasStored)
        {
            incomingProperty.Value = stored;
            return;
        }

        AddError(errors, path, $"\"{value}\" is reserved to mean \"keep the stored value\" and cannot be stored as a value. There is no stored value to keep here, so send the real value or clear the property instead.");
    }

    private static void CollectSecrets(JToken token, Type declaredType, string shape, byte[] key, Dictionary<string, string> pool)
    {
        if (GetDictionaryValueType(declaredType) is { } dictionaryValueType)
        {
            if (token is JObject map)
                foreach (var entry in map.Properties())
                    CollectSecrets(entry.Value, dictionaryValueType, shape, key, pool);
            return;
        }

        if (GetEnumerableElementType(declaredType) is { } elementType)
        {
            if (token is JArray array)
                foreach (var item in array)
                    CollectSecrets(item, elementType, shape, key, pool);
            return;
        }

        if (token is not JObject obj || !IsWalkable(declaredType))
            return;

        foreach (var (jsonName, property) in GetSerializableProperties(declaredType))
        {
            if (obj.Property(jsonName, StringComparison.OrdinalIgnoreCase)?.Value is not { } value)
                continue;

            var propertyShape = Join(shape, property.Name);
            if (IsSecret(property))
            {
                if (value.Type is JTokenType.String && value.Value<string>() is { Length: > 0 } secret)
                    pool[PoolKey(propertyShape, GetFingerprint(secret, key))] = secret;
                continue;
            }

            CollectSecrets(value, property.PropertyType, propertyShape, key, pool);
        }
    }

    private static string PoolKey(string shape, string fingerprint)
        => $"{shape}\n{fingerprint}";

    private static void AddError(Dictionary<string, List<string>> errors, string path, string message)
    {
        if (!errors.TryGetValue(path, out var messages))
            errors[path] = messages = [];
        messages.Add(message);
    }

    private static string Join(string path, string name)
        => string.IsNullOrEmpty(path) ? name : $"{path}.{name}";

    #endregion

    #region Fingerprints

    private static void ValidateKey(byte[] key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Length is 0)
            throw new ArgumentException("The fingerprint key cannot be empty.", nameof(key));
    }

    private static string GetFingerprint(string secret, byte[] key)
        => Base64Url.EncodeToString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(secret)).AsSpan(0, FingerprintLength));

    private static bool TryGetFingerprint(string? value, [NotNullWhen(true)] out string? fingerprint)
    {
        fingerprint = value is not null &&
            value.Length > MaskedPrefix.Length + MaskedSuffix.Length &&
            value.StartsWith(MaskedPrefix, StringComparison.Ordinal) &&
            value.EndsWith(MaskedSuffix, StringComparison.Ordinal)
            ? value[MaskedPrefix.Length..^MaskedSuffix.Length]
            : null;
        return fingerprint is not null;
    }

    #endregion

    #region Reflection

    private static bool IsSecret(PropertyInfo property)
        => property.GetCustomAttribute<PasswordPropertyTextAttribute>(inherit: true) is not null ||
            property.GetCustomAttribute<DataTypeAttribute>(inherit: true) is { DataType: DataType.Password };

    private static bool ContainsSecrets(Type type, HashSet<Type> visiting)
    {
        if (_containsSecrets.TryGetValue(type, out var cached))
            return cached;

        if (GetDictionaryValueType(type) is { } dictionaryValueType)
            return ContainsSecrets(dictionaryValueType, visiting);

        if (GetEnumerableElementType(type) is { } elementType)
            return ContainsSecrets(elementType, visiting);

        if (!IsWalkable(type))
            return false;

        // Guard against a type graph that refers back to itself. Such a cycle
        // adds no new properties, so bailing out of it cannot hide a secret.
        if (!visiting.Add(type))
            return false;

        var result = false;
        foreach (var (_, property) in GetSerializableProperties(type))
        {
            if (IsSecret(property) || ContainsSecrets(property.PropertyType, visiting))
            {
                result = true;
                break;
            }
        }

        visiting.Remove(type);

        // Only a walk that did not bail out of a cycle is worth remembering.
        if (visiting.Count is 0)
            _containsSecrets[type] = result;

        return result;
    }

    private static IReadOnlyDictionary<string, PropertyInfo> GetSerializableProperties(Type type)
        => _serializableProperties.GetOrAdd(type, static key =>
        {
            var properties = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in key.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (property.GetIndexParameters().Length > 0 || property.GetMethod is null)
                    continue;

                // A property neither serializer writes cannot leak, and injecting a
                // value for it on the way back in would only add a member that the
                // deserializer then drops.
                if (property.GetCustomAttribute<NewtonsoftJsonIgnoreAttribute>(inherit: true) is not null ||
                    property.GetCustomAttribute<SystemTextJsonIgnoreAttribute>(inherit: true) is not null)
                    continue;

                var name = property.GetCustomAttribute<NewtonsoftJsonPropertyAttribute>(inherit: true)?.PropertyName
                    ?? property.GetCustomAttribute<SystemTextJsonPropertyNameAttribute>(inherit: true)?.Name
                    ?? property.Name;
                properties.TryAdd(name, property);
            }
            return properties;
        });

    private static Type? GetDictionaryValueType(Type type)
    {
        if (type.IsInterface && type.IsGenericType && type.GetGenericTypeDefinition() is { } definition &&
            (definition == typeof(IDictionary<,>) || definition == typeof(IReadOnlyDictionary<,>)))
            return type.GetGenericArguments()[1];

        foreach (var contract in type.GetInterfaces())
        {
            if (!contract.IsGenericType)
                continue;

            var contractDefinition = contract.GetGenericTypeDefinition();
            if (contractDefinition == typeof(IDictionary<,>) || contractDefinition == typeof(IReadOnlyDictionary<,>))
                return contract.GetGenericArguments()[1];
        }

        return null;
    }

    private static Type? GetEnumerableElementType(Type type)
    {
        if (type == typeof(string))
            return null;

        if (type.IsArray)
            return type.GetElementType();

        if (type.IsInterface && type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            return type.GetGenericArguments()[0];

        foreach (var contract in type.GetInterfaces())
        {
            if (contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                return contract.GetGenericArguments()[0];
        }

        return null;
    }

    private static bool IsWalkable(Type type)
    {
        if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal))
            return false;

        if (Nullable.GetUnderlyingType(type) is not null)
            return false;

        // Framework types have no configuration properties of their own, and
        // walking them only wastes reflection on things like DateTime.Ticks.
        return type.Namespace is { } nameSpace &&
            !nameSpace.Equals("System", StringComparison.Ordinal) &&
            !nameSpace.StartsWith("System.", StringComparison.Ordinal) &&
            !nameSpace.Equals("Microsoft", StringComparison.Ordinal) &&
            !nameSpace.StartsWith("Microsoft.", StringComparison.Ordinal) &&
            !nameSpace.Equals("Newtonsoft", StringComparison.Ordinal) &&
            !nameSpace.StartsWith("Newtonsoft.", StringComparison.Ordinal);
    }

    #endregion
}
