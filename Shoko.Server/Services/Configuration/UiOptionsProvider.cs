using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Namotion.Reflection;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.UI;
using Shoko.Abstractions.UI.Attributes;
using Shoko.Abstractions.UI.Components;
using Shoko.Abstractions.UI.Enums;
using Shoko.Server.Extensions;
using Shoko.Server.Plugin;
using Shoko.Server.Utilities;

namespace Shoko.Server.Services.Configuration;

/// <summary>
///   Finds, checks and runs the method an <see cref="OptionsProviderAttribute"/>
///   names, for a configuration and an executable action alike.
/// </summary>
/// <remarks>
///   The same shapes are reported at the authoring site by the SHOKO0008 to
///   SHOKO0013 analyzer rules; the check here is for a plugin built without
///   them.
/// </remarks>
internal static class UiOptionsProvider
{
    #region Validation

    private static readonly ConcurrentDictionary<Type, IReadOnlyDictionary<(string Member, OptionsTarget Target), MethodInfo>> _providers = new();

    /// <summary>
    ///   Finds every options provider a type declares, and checks that each
    ///   lists values every member it names can take.
    /// </summary>
    /// <param name="owner">The type declaring the providers and their members.</param>
    /// <returns>The provider of each member part that has one.</returns>
    /// <exception cref="NotSupportedException">
    ///   Thrown when a provider is not public, is generic, names a member the
    ///   type does not have, one that cannot take options or one another
    ///   provider claimed, names members of different option types, or returns
    ///   something else.
    /// </exception>
    public static IReadOnlyDictionary<(string Member, OptionsTarget Target), MethodInfo> GetProviders(Type owner)
        => _providers.GetOrAdd(owner, CollectProviders);

    private static IReadOnlyDictionary<(string Member, OptionsTarget Target), MethodInfo> CollectProviders(Type owner)
    {
        var providers = new Dictionary<(string Member, OptionsTarget Target), MethodInfo>();
        var methods = owner.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy);
        foreach (var method in methods)
        {
            if (method.GetCustomAttribute<OptionsProviderAttribute>(true) is not { } attribute)
                continue;

            var declaredBy = $"{owner.Name}.{method.Name}";
            if (!method.IsPublic)
                throw Invalid(declaredBy, "is not public");
            if (method.IsGenericMethodDefinition)
                throw Invalid(declaredBy, "is generic");
            if (attribute.Members is not { Length: > 0 } members)
                throw Invalid(declaredBy, "names no members");

            var (optionType, firstMember) = ((Type?)null, (string?)null);
            foreach (var member in members)
            {
                if (owner.GetProperty(member, BindingFlags.Public | BindingFlags.Instance) is not { } property)
                    throw Invalid(declaredBy, $"names \"{member}\", which {owner.Name} does not have");
                if (GetOptionType(property.PropertyType, attribute.Target, out var shapeFailure) is not { } memberOptionType)
                    throw Invalid(declaredBy, $"names \"{member}\", which {shapeFailure}");
                if (!IsAllowedOptionType(memberOptionType))
                    throw Invalid(
                        declaredBy,
                        $"names \"{member}\", whose options would be {ShokoJsonSchemaGenerator.GetFriendlyTypeName(memberOptionType)}, which is neither a primitive nor convertible to and from one"
                    );
                if (providers.TryGetValue((member, attribute.Target), out var other))
                    throw Invalid(
                        declaredBy,
                        other == method ? $"names \"{member}\" twice" : $"names \"{member}\", whose {Noun(attribute.Target)} {other.Name} already provides for"
                    );
                if (optionType is null)
                {
                    (optionType, firstMember) = (memberOptionType, member);
                }
                else if (memberOptionType != optionType)
                {
                    var firstName = ShokoJsonSchemaGenerator.GetFriendlyTypeName(optionType);
                    var otherName = ShokoJsonSchemaGenerator.GetFriendlyTypeName(memberOptionType);
                    throw Invalid(declaredBy, $"names \"{firstMember}\" and \"{member}\", whose options are {firstName} and {otherName}");
                }

                providers[(member, attribute.Target)] = method;
            }

            var returned = GetReturnedOptionType(method.ReturnType);
            if (returned != optionType && returned != typeof(SelectOption<>).TryMakeGenericType(optionType!))
            {
                var returnName = ShokoJsonSchemaGenerator.GetFriendlyTypeName(method.ReturnType);
                var optionName = ShokoJsonSchemaGenerator.GetFriendlyTypeName(optionType!);
                throw Invalid(
                    declaredBy,
                    returned is null ? $"returns {returnName}, which is not a collection of options" : $"returns {returnName} rather than a collection of {optionName}"
                );
            }
        }

        return providers;
    }

    /// <summary>
    ///   The type of a single option for a part of a member: a list offers
    ///   options for its entries, a dictionary for its values or keys, and a
    ///   nullable value for what it wraps.
    /// </summary>
    /// <param name="memberType">The member's type.</param>
    /// <param name="target">The part of the member the options are for.</param>
    /// <param name="failure">Why the member takes no options, when it cannot.</param>
    /// <returns>The option type, or <c>null</c> when the member cannot take options.</returns>
    internal static Type? GetOptionType(Type memberType, OptionsTarget target, out string? failure)
    {
        failure = null;
        var type = Unwrap(memberType);
        var dictionary = GetDictionaryTypes(type);
        if (dictionary is null && typeof(IDictionary).IsAssignableFrom(type))
        {
            failure = "is a dictionary without key and value types";
            return null;
        }

        if (dictionary is null)
        {
            if (target is OptionsTarget.Keys)
            {
                failure = "is not a dictionary, so it has no keys";
                return null;
            }

            return GetValueOptionType(type, out failure);
        }

        if (target is OptionsTarget.Keys)
            return Unwrap(dictionary.Value.Key);

        var valueType = Unwrap(dictionary.Value.Value);
        if (GetDictionaryTypes(valueType) is not null || typeof(IDictionary).IsAssignableFrom(valueType))
        {
            failure = "holds dictionaries, which have no single value to offer options for";
            return null;
        }

        return GetValueOptionType(valueType, out failure);
    }

    /// <summary>
    ///   The option type of a value that is not a dictionary: itself, or a
    ///   list's entries.
    /// </summary>
    private static Type? GetValueOptionType(Type type, out string? failure)
    {
        failure = null;
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(SelectComponent<>))
        {
            failure = "is a select component, which carries its own options";
            return null;
        }

        if (type == typeof(string))
            return type;

        var element = type.IsArray ? type.GetElementType()! : FindGeneric(type, typeof(IEnumerable<>))?.GetGenericArguments()[0];
        return element is null ? type : Unwrap(element);
    }

    /// <summary>
    ///   The key and value types of a generic dictionary, or <c>null</c> when
    ///   the type is not one.
    /// </summary>
    private static (Type Key, Type Value)? GetDictionaryTypes(Type type)
    {
        if ((FindGeneric(type, typeof(IDictionary<,>)) ?? FindGeneric(type, typeof(IReadOnlyDictionary<,>))) is { } dictionary)
            return (dictionary.GetGenericArguments()[0], dictionary.GetGenericArguments()[1]);

        return null;
    }

    /// <summary>
    ///   Whether options of the type can be told apart and labelled as text: a
    ///   primitive, a string, a decimal, an enum, one of the common value types
    ///   that round-trip through text, or a type of any origin that is either
    ///   parsable from text or has a type converter to and from text or one of
    ///   the primitives.
    /// </summary>
    private static bool IsAllowedOptionType(Type type)
        => type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) || type == typeof(Guid) ||
            type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(DateOnly) || type == typeof(TimeOnly) ||
            type == typeof(TimeSpan) || type == typeof(Uri) ||
            type.GetInterfaces().Any(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IParsable<>) && x.GetGenericArguments()[0] == type) ||
            HasConverter(type);

    private static readonly Type[] _convertibleTypes =
    [
        typeof(string), typeof(bool), typeof(char), typeof(sbyte), typeof(byte), typeof(short), typeof(ushort), typeof(int),
        typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal),
    ];

    /// <summary>
    ///   Whether the type names a type converter that turns it into text or a
    ///   primitive and back again.
    /// </summary>
    private static bool HasConverter(Type type)
        => type.GetCustomAttribute<TypeConverterAttribute>(true) is not null &&
            TypeDescriptor.GetConverter(type) is { } converter &&
            _convertibleTypes.Any(other => converter.CanConvertFrom(other) && converter.CanConvertTo(other));

    private static Type Unwrap(Type type)
        => Nullable.GetUnderlyingType(type) ?? type;

    private static string Noun(OptionsTarget target)
        => target is OptionsTarget.Keys ? "keys" : "values";

    /// <summary>
    ///   The type of a single option a method lists, looking through a task.
    /// </summary>
    private static Type? GetReturnedOptionType(Type returnType)
    {
        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() is var definition &&
            (definition == typeof(Task<>) || definition == typeof(ValueTask<>)))
            returnType = returnType.GetGenericArguments()[0];
        if (returnType == typeof(string))
            return null;

        // A nullable entry is listed for the type it wraps, its nulls skipped.
        var element = returnType.IsArray ? returnType.GetElementType() : FindGeneric(returnType, typeof(IEnumerable<>))?.GetGenericArguments()[0];
        return element is null ? null : Unwrap(element);
    }

    private static Type? FindGeneric(Type type, Type definition)
        => type.IsGenericType && type.GetGenericTypeDefinition() == definition
            ? type
            : type.GetInterfaces().FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == definition);

    private static Type? TryMakeGenericType(this Type definition, Type argument)
    {
        try
        {
            return definition.MakeGenericType(argument);
        }
        catch (ArgumentException)
        {
            // The argument breaks a constraint, so nothing can be returned as one.
            return null;
        }
    }

    private static NotSupportedException Invalid(string declaredBy, string failure)
        => new($"The options provider '{declaredBy}' {failure}.");

    #endregion

    #region Lookup

    /// <summary>
    ///   Walks a path to the member it names, and returns the instance holding
    ///   it along with the method that member takes its options from.
    /// </summary>
    /// <param name="root">The configuration or action instance.</param>
    /// <param name="path">The member's path, as a custom action is invoked with.</param>
    /// <param name="target">The part of the member the options are for.</param>
    /// <param name="isNewtonsoftJson">Whether the path uses the Newtonsoft member names.</param>
    /// <returns>The instance holding the member, and the method.</returns>
    /// <exception cref="ArgumentException">
    ///   Thrown when the path does not lead to a member that takes options.
    /// </exception>
    public static (object Owner, MethodInfo Method) Resolve(object root, string path, OptionsTarget target, bool isNewtonsoftJson)
    {
        var parts = ConfigurationService.SplitPath(path);
        if (parts.Length is 0 || parts[^1].StartsWith('['))
            throw new ArgumentException($"Invalid path \"{path}\"", nameof(path));

        var owner = root;
        foreach (var part in parts[..^1])
        {
            owner = Step(owner, part) ?? throw new ArgumentException($"Invalid path \"{path}\"", nameof(path));
        }

        if (FindProperty(owner.GetType(), parts[^1], isNewtonsoftJson) is not { } property)
            throw new ArgumentException($"Invalid path \"{path}\"", nameof(path));
        if (!GetProviders(property.ReflectedType!).TryGetValue((property.Name, target), out var method))
            throw new ArgumentException($"The {Noun(target)} of the member at \"{path}\" take no options", nameof(path));

        return (owner, method);

        object? Step(object value, string part)
        {
            if (part.Length > 4 && part.StartsWith("[\"") && part.EndsWith("\"]"))
            {
                var key = part[2..^2];
                return value is IDictionary dictionary && dictionary.Contains(key) ? dictionary[key] : null;
            }

            if (part.Length > 2 && part[0] is '[' && part[^1] is ']')
                return int.TryParse(part[1..^1], out var index) && value is IEnumerable enumerable
                    ? enumerable.Cast<object?>().ElementAtOrDefault(index)
                    : null;

            return FindProperty(value.GetType(), part, isNewtonsoftJson)?.GetValue(value);
        }
    }

    private static PropertyInfo? FindProperty(Type type, string name, bool isNewtonsoftJson)
        => type.ToContextualType().Properties
            .FirstOrDefault(x => string.Equals(ConfigurationService.GetJsonName(x, isNewtonsoftJson), name, StringComparison.Ordinal))
            ?.PropertyInfo;

    #endregion

    #region Invocation

    /// <summary>
    ///   Runs an options method and serialises what it listed.
    /// </summary>
    /// <param name="method">The method, as <see cref="GetProviders"/> found it.</param>
    /// <param name="pluginManager">Resolves the parameters no argument fills.</param>
    /// <param name="owner">The instance declaring the member.</param>
    /// <param name="arguments">The values the method's parameters may take.</param>
    /// <param name="convert">Serialises a value the way its owner would.</param>
    /// <returns>
    ///   The options, in the order the method listed them, without those of a
    ///   flags enum that are not one single-bit member.
    /// </returns>
    public static async Task<IReadOnlyList<UiOption>> InvokeAsync(
        MethodInfo method,
        IPluginManager pluginManager,
        object owner,
        IEnumerable<object?> arguments,
        Func<object?, JToken?> convert
    )
    {
        object? result;
        try
        {
            result = method.Invoke<object>(pluginManager, owner, arguments);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            // A provider's own exception, a refusal among them, surfaces as
            // it was thrown rather than wrapped by reflection.
            ExceptionDispatchInfo.Throw(ex.InnerException);
            throw;
        }

        if (result is not null && result.GetType() is { IsGenericType: true } resultType && resultType.GetGenericTypeDefinition() == typeof(ValueTask<>))
            result = resultType.GetMethod(nameof(ValueTask<object>.AsTask))!.Invoke(result, null);
        if (result is Task task)
        {
            await task.ConfigureAwait(false);
            result = task.GetType().GetProperty(nameof(Task<object>.Result))!.GetValue(task);
        }

        if (result is not IEnumerable values)
            return [];

        var options = new List<UiOption>();
        // Nulls are skipped, and everything else is kept in the order listed,
        // duplicates included.
        foreach (var value in values.Cast<object?>())
        {
            if (value is null)
                continue;

            if (value.GetType() is { IsGenericType: true } valueType && valueType.GetGenericTypeDefinition() == typeof(SelectOption<>))
            {
                if (valueType.GetProperty(nameof(SelectOption<int>.Value))!.GetValue(value) is not { } optionValue)
                    continue;

                if (ConvertOption(optionValue, convert) is not { } optionToken)
                    continue;

                options.Add(new()
                {
                    Value = optionToken,
                    Label = (string?)valueType.GetProperty(nameof(SelectOption<int>.Label))!.GetValue(value) ?? Stringify(optionValue),
                });
                continue;
            }

            if (ConvertOption(value, convert) is not { } token)
                continue;

            options.Add(new() { Value = token, Label = Stringify(value) });
        }

        return options;
    }

    /// <summary>
    ///   Serialises an option. An option of a flags enum is an entry of its
    ///   list, so it is the one name of a single-bit member, and any other
    ///   value of it is skipped.
    /// </summary>
    private static JToken? ConvertOption(object value, Func<object?, JToken?> convert)
    {
        var token = convert(value);
        if (!FlagEnums.IsFlagEnum(value.GetType()))
            return token;

        return token is JArray { Count: 1 } list ? list[0] : null;
    }

    /// <summary>
    ///   The label of a value its provider gave none for.
    /// </summary>
    private static string Stringify(object value)
        => value switch
        {
            // An enum reads the way the form's own list of its members does.
            Enum when Enum.GetName(value.GetType(), value) is { } name
                => TypeReflectionExtensions.GetDisplayName(value.GetType().ToContextualType().GetField(name)!),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ when HasConverter(value.GetType()) && TypeDescriptor.GetConverter(value) is { } converter && converter.CanConvertTo(typeof(string))
                => converter.ConvertToInvariantString(value) ?? string.Empty,
            _ => value.ToString() ?? string.Empty,
        };

    #endregion
}
