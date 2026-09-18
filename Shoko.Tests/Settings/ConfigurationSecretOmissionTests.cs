using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Shoko.Abstractions.Config;
using Shoko.Server.Settings;
using Xunit;

using NewtonsoftJsonIgnoreAttribute = Newtonsoft.Json.JsonIgnoreAttribute;
using SystemTextJsonIgnoreAttribute = System.Text.Json.Serialization.JsonIgnoreAttribute;

namespace Shoko.Tests.Settings;

/// <summary>
/// Guards the secret-masking mechanism against the failure that actually
/// matters: not the credentials that carry
/// <see cref="PasswordPropertyTextAttribute"/> today, but the one somebody adds
/// six months from now without it. Masking only ever covers marked properties,
/// so an unmarked credential is serialized in the clear to every client of the
/// v3 configuration API and to every plugin that hands a configuration to
/// something else.
/// </summary>
public class ConfigurationSecretOmissionTests
{
    /// <summary>
    /// Property names that read as a credential. Deliberately broad: a false
    /// positive costs one allowlist entry with a reason next to it, a false
    /// negative costs a leak nobody notices.
    /// </summary>
    private static readonly Regex _secretNamePattern = new(
        "password|passphrase|token|secret|credential|key",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// Properties whose names trip <see cref="_secretNamePattern"/> but that
    /// hold no credential. Every entry is a deliberate decision, and adding one
    /// is the only way past this test other than marking the property.
    /// </summary>
    /// <remarks>
    /// Keyed by "DeclaringType.PropertyName".
    /// </remarks>
    private static readonly IReadOnlyDictionary<string, string> _allowlist = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        // Empty on purpose. Every configuration property that reads as a
        // credential today is marked. Add an entry here, with the reason, only
        // when a property genuinely is not one.
    };

    public static TheoryData<Type> ConfigurationTypes
    {
        get
        {
            var data = new TheoryData<Type>();
            foreach (var type in GetConfigurationTypes())
                data.Add(type);
            return data;
        }
    }

    [Fact]
    public void ConfigurationTypes_AreDiscovered()
    {
        var types = GetConfigurationTypes().ToList();

        // A mis-wired discovery would make every other case in this file pass
        // vacuously, so assert that we really are looking at the server's
        // configurations.
        Assert.Contains(typeof(ServerSettings), types);
        Assert.True(types.Count > 1, "Expected more than one configuration type to be discovered.");
    }

    [Theory]
    [MemberData(nameof(ConfigurationTypes))]
    public void EverySecretLookingProperty_IsMarkedOrAllowlisted(Type configurationType)
    {
        var offenders = new List<string>();
        Walk(configurationType, [], offenders);

        Assert.True(offenders.Count is 0, string.Join(
            Environment.NewLine,
            [
                $"{configurationType.Name} has properties that read as credentials but are not marked with [PasswordPropertyText]:",
                .. offenders.Select(offender => $"  - {offender}"),
                "Mark each one so it is masked on its way out of the server, or add it to the allowlist in this test with a reason.",
            ]));
    }

    [Theory]
    [MemberData(nameof(ConfigurationTypes))]
    public void EveryMarkedProperty_IsAStringTheMaskerCanReplace(Type configurationType)
    {
        var offenders = new List<string>();
        WalkMarked(configurationType, [], offenders);

        // The masker replaces a secret with a string sentinel, so a marked
        // property that is not a string would either be dropped on the way out or
        // fail to deserialize on the way back in.
        Assert.True(offenders.Count is 0, string.Join(
            Environment.NewLine,
            [
                $"{configurationType.Name} marks non-string properties with [PasswordPropertyText]:",
                .. offenders.Select(offender => $"  - {offender}"),
            ]));
    }

    #region Walking

    private static void Walk(Type type, HashSet<Type> visiting, List<string> offenders, string path = "")
    {
        if (!visiting.Add(type))
            return;

        foreach (var property in GetSerializableProperties(type))
        {
            var propertyPath = string.IsNullOrEmpty(path) ? property.Name : $"{path}.{property.Name}";
            if (_secretNamePattern.IsMatch(property.Name) &&
                property.GetCustomAttribute<PasswordPropertyTextAttribute>(inherit: true) is null &&
                // A property marked [Key] is the identity a list element is paired
                // up by. It is compared in the clear by design, so it can never be
                // a secret.
                property.GetCustomAttribute<KeyAttribute>(inherit: true) is null &&
                !_allowlist.ContainsKey($"{property.DeclaringType!.Name}.{property.Name}"))
                offenders.Add($"{propertyPath} ({property.DeclaringType.Name}.{property.Name})");

            if (Unwrap(property.PropertyType) is { } inner && IsWalkable(inner))
                Walk(inner, visiting, offenders, propertyPath);
        }

        visiting.Remove(type);
    }

    private static void WalkMarked(Type type, HashSet<Type> visiting, List<string> offenders, string path = "")
    {
        if (!visiting.Add(type))
            return;

        foreach (var property in GetSerializableProperties(type))
        {
            var propertyPath = string.IsNullOrEmpty(path) ? property.Name : $"{path}.{property.Name}";
            if (property.GetCustomAttribute<PasswordPropertyTextAttribute>(inherit: true) is not null && property.PropertyType != typeof(string))
                offenders.Add($"{propertyPath} is {property.PropertyType.Name}");

            if (Unwrap(property.PropertyType) is { } inner && IsWalkable(inner))
                WalkMarked(inner, visiting, offenders, propertyPath);
        }

        visiting.Remove(type);
    }

    private static IEnumerable<Type> GetConfigurationTypes()
        => new[] { typeof(ServerSettings).Assembly, typeof(IConfiguration).Assembly }
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsClass: true, IsAbstract: false } && type.IsAssignableTo(typeof(IConfiguration)))
            .OrderBy(type => type.FullName, StringComparer.Ordinal);

    private static IEnumerable<PropertyInfo> GetSerializableProperties(Type type)
        => type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.GetIndexParameters().Length is 0 && property.GetMethod is not null)
            // A property neither serializer writes cannot leak through a
            // configuration document.
            .Where(property => property.GetCustomAttribute<NewtonsoftJsonIgnoreAttribute>(inherit: true) is null)
            .Where(property => property.GetCustomAttribute<SystemTextJsonIgnoreAttribute>(inherit: true) is null);

    private static Type? Unwrap(Type type)
    {
        if (type == typeof(string))
            return null;

        if (Nullable.GetUnderlyingType(type) is { } underlying)
            return Unwrap(underlying);

        if (type.IsArray)
            return type.GetElementType() is { } elementType ? Unwrap(elementType) ?? elementType : null;

        foreach (var contract in type.GetInterfaces().Prepend(type))
        {
            if (!contract.IsGenericType)
                continue;

            var definition = contract.GetGenericTypeDefinition();
            if (definition == typeof(IDictionary<,>) || definition == typeof(IReadOnlyDictionary<,>))
                return contract.GetGenericArguments()[1] is { } valueType ? Unwrap(valueType) ?? valueType : null;

            if (definition == typeof(IEnumerable<>))
                return contract.GetGenericArguments()[0] is { } itemType ? Unwrap(itemType) ?? itemType : null;
        }

        return type;
    }

    private static bool IsWalkable(Type type)
        => !type.IsPrimitive && !type.IsEnum && type != typeof(string) && type != typeof(decimal) &&
            type.Namespace is { } nameSpace &&
            !nameSpace.Equals("System", StringComparison.Ordinal) &&
            !nameSpace.StartsWith("System.", StringComparison.Ordinal) &&
            !nameSpace.StartsWith("Microsoft.", StringComparison.Ordinal) &&
            !nameSpace.StartsWith("Newtonsoft.", StringComparison.Ordinal);

    #endregion
}
