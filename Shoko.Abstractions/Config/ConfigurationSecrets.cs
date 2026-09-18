using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
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
///     <see cref="PasswordPropertyTextAttribute"/>, the same marker the schema
///     generator uses to render a password field and
///     <c>SettingsProvider.DumpSettings</c> uses to keep credentials out of the
///     log.
///   </para>
///   <para>
///     On the way out, a secret that holds a value is replaced with
///     <see cref="Sentinel"/>. A secret that holds <c>null</c> or an empty
///     string is left alone, so "nothing configured" stays distinguishable from
///     "withheld".
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
///     <item><description>the property equals <see cref="Sentinel"/> — a masked value round-tripped unchanged; the stored value is kept;</description></item>
///     <item><description>the property holds some other value — the user changed it; the incoming value wins;</description></item>
///     <item><description>the property is explicitly <c>null</c> or an empty string — the user cleared it; the secret is cleared.</description></item>
///   </list>
///   <para>
///     The last case is why simply skipping the sentinel on deserialize would
///     not do: clearing a secret has to stay possible, and has to stay
///     distinguishable from leaving it alone.
///   </para>
///   <para>
///     Restoring a secret nested inside a collection needs each incoming element
///     paired with the element that holds the stored value, and the position in
///     a list cannot do that: reorder the list and every secret lands on the
///     wrong element. A dictionary pairs up by its key. A list pairs up by the
///     element property marked with <see cref="KeyAttribute"/>, the same marker
///     the schema generator already uses to tell the UI which field identifies a
///     record. A list whose elements carry no such property cannot be paired up
///     at all, and rather than guess, <see cref="Restore"/> rejects a sentinel
///     arriving inside one and says the real value is required there.
///   </para>
/// </remarks>
public static class ConfigurationSecrets
{
    #region Constants

    /// <summary>
    ///   The value a masked secret is replaced with on the wire.
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

    #endregion

    #region Caches

    private static readonly ConcurrentDictionary<Type, IReadOnlyDictionary<string, PropertyInfo>> _serializableProperties = [];

    private static readonly ConcurrentDictionary<Type, string?> _identityNames = [];

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
    ///   Replaces every set secret in <paramref name="token"/> with
    ///   <see cref="Sentinel"/>.
    /// </summary>
    /// <param name="token">
    ///   The serialized configuration. It is not modified; a masked copy is
    ///   returned.
    /// </param>
    /// <param name="type">
    ///   The configuration type the document was serialized from.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when <paramref name="token"/> or <paramref name="type"/> is
    ///   <c>null</c>.
    /// </exception>
    /// <returns>
    ///   A masked copy of the document.
    /// </returns>
    public static JToken Mask(JToken token, Type type)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(type);

        var masked = token.DeepClone();
        MaskValue(masked, type);
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
    ///   incoming <see cref="Sentinel"/> is reported as an error instead.
    /// </param>
    /// <param name="type">
    ///   The configuration type both documents belong to.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when <paramref name="incoming"/> or <paramref name="type"/> is
    ///   <c>null</c>.
    /// </exception>
    /// <returns>
    ///   The restored copy, and any errors keyed by the property path that
    ///   produced them. A non-empty error set means the document must be
    ///   rejected rather than saved.
    /// </returns>
    public static (JToken Token, IReadOnlyDictionary<string, IReadOnlyList<string>> Errors) Restore(JToken incoming, JToken? current, Type type)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        ArgumentNullException.ThrowIfNull(type);

        var restored = incoming.DeepClone();
        var errors = new Dictionary<string, List<string>>();
        RestoreValue(restored, current, type, string.Empty, errors, withinUnidentifiedList: false);
        return (restored, errors.ToDictionary(a => a.Key, a => (IReadOnlyList<string>)a.Value));
    }

    #endregion

    #region Masking

    private static void MaskValue(JToken token, Type declaredType)
    {
        if (GetDictionaryValueType(declaredType) is { } dictionaryValueType)
        {
            if (token is JObject map)
                foreach (var entry in map.Properties())
                    MaskValue(entry.Value, dictionaryValueType);
            return;
        }

        if (GetEnumerableElementType(declaredType) is { } elementType)
        {
            if (token is JArray array)
                foreach (var item in array)
                    MaskValue(item, elementType);
            return;
        }

        if (token is JObject obj && IsWalkable(declaredType))
            MaskObject(obj, declaredType);
    }

    private static void MaskObject(JObject obj, Type type)
    {
        var properties = GetSerializableProperties(type);
        foreach (var jsonProperty in obj.Properties())
        {
            if (!properties.TryGetValue(jsonProperty.Name, out var property))
                continue;

            if (IsSecret(property))
            {
                if (jsonProperty.Value.Type is JTokenType.String && !string.IsNullOrEmpty(jsonProperty.Value.Value<string>()))
                    jsonProperty.Value = Sentinel;
                continue;
            }

            MaskValue(jsonProperty.Value, property.PropertyType);
        }
    }

    #endregion

    #region Restoring

    private static void RestoreValue(JToken incoming, JToken? current, Type declaredType, string path, Dictionary<string, List<string>> errors, bool withinUnidentifiedList)
    {
        if (!ContainsSecrets(declaredType))
            return;

        if (GetDictionaryValueType(declaredType) is { } dictionaryValueType)
        {
            if (incoming is not JObject map)
                return;

            // A dictionary key is an identity by construction, so entries pair up
            // without needing a marked property on the value type.
            var currentMap = current as JObject;
            foreach (var entry in map.Properties())
                RestoreValue(entry.Value, currentMap?.Property(entry.Name, StringComparison.Ordinal)?.Value, dictionaryValueType, Join(path, entry.Name), errors, withinUnidentifiedList);
            return;
        }

        if (GetEnumerableElementType(declaredType) is { } elementType)
        {
            if (incoming is not JArray array)
                return;

            var identityName = GetIdentityName(elementType);
            var currentArray = current as JArray;
            for (var index = 0; index < array.Count; index++)
            {
                var elementPath = $"{path}[{index}]";
                if (identityName is null)
                {
                    // Nothing on the element identifies it across a reorder, so we
                    // refuse to guess which stored element a masked secret belongs
                    // to. Anything already unmasked passes straight through.
                    RestoreValue(array[index], null, elementType, elementPath, errors, withinUnidentifiedList: true);
                    continue;
                }

                var identity = (array[index] as JObject)?.Property(identityName, StringComparison.OrdinalIgnoreCase)?.Value;
                var match = identity is null
                    ? null
                    : currentArray?
                        .OfType<JObject>()
                        .FirstOrDefault(element => JToken.DeepEquals(element.Property(identityName, StringComparison.OrdinalIgnoreCase)?.Value, identity));
                RestoreValue(array[index], match, elementType, elementPath, errors, withinUnidentifiedList);
            }
            return;
        }

        if (incoming is JObject obj && IsWalkable(declaredType))
            RestoreObject(obj, current as JObject, declaredType, path, errors, withinUnidentifiedList);
    }

    private static void RestoreObject(JObject incoming, JObject? current, Type type, string path, Dictionary<string, List<string>> errors, bool withinUnidentifiedList)
    {
        foreach (var (jsonName, property) in GetSerializableProperties(type))
        {
            var incomingProperty = incoming.Property(jsonName, StringComparison.OrdinalIgnoreCase);
            var currentValue = current?.Property(jsonName, StringComparison.OrdinalIgnoreCase)?.Value;
            var propertyPath = Join(path, property.Name);
            if (IsSecret(property))
            {
                RestoreSecret(incoming, incomingProperty, jsonName, currentValue, propertyPath, errors, withinUnidentifiedList);
                continue;
            }

            if (incomingProperty is not null)
                RestoreValue(incomingProperty.Value, currentValue, property.PropertyType, propertyPath, errors, withinUnidentifiedList);
        }
    }

    private static void RestoreSecret(JObject incoming, JProperty? incomingProperty, string jsonName, JToken? currentValue, string path, Dictionary<string, List<string>> errors, bool withinUnidentifiedList)
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

        // Anything that is not the sentinel is the sender's own intent: an
        // explicit null or empty string clears the secret, any other value
        // replaces it. Both stand as sent.
        if (incomingProperty.Value.Type is not JTokenType.String || !string.Equals(incomingProperty.Value.Value<string>(), Sentinel, StringComparison.Ordinal))
            return;

        if (hasStored)
        {
            incomingProperty.Value = stored;
            return;
        }

        AddError(errors, path, withinUnidentifiedList
            ? $"\"{Sentinel}\" cannot be restored here because the list element has no property marked with [Key] to match it against a stored element. Send the real value for this property instead."
            : $"\"{Sentinel}\" is reserved to mean \"keep the stored value\" and cannot be stored as a value. There is no stored value to keep here, so send the real value or clear the property instead.");
    }

    private static void AddError(Dictionary<string, List<string>> errors, string path, string message)
    {
        if (!errors.TryGetValue(path, out var messages))
            errors[path] = messages = [];
        messages.Add(message);
    }

    private static string Join(string path, string name)
        => string.IsNullOrEmpty(path) ? name : $"{path}.{name}";

    #endregion

    #region Reflection

    private static bool IsSecret(PropertyInfo property)
        => property.GetCustomAttribute<PasswordPropertyTextAttribute>(inherit: true) is not null;

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

    private static string? GetIdentityName(Type type)
        => _identityNames.GetOrAdd(type, static key => GetSerializableProperties(key)
            .Where(pair => pair.Value.GetCustomAttribute<KeyAttribute>(inherit: true) is not null)
            .Select(pair => pair.Key)
            .FirstOrDefault());

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
