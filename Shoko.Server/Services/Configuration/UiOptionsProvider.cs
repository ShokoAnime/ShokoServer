using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Namotion.Reflection;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.UI;
using Shoko.Abstractions.UI.Attributes;
using Shoko.Abstractions.UI.Components;
using Shoko.Server.Extensions;

namespace Shoko.Server.Services.Configuration;

/// <summary>
///   Finds, checks and runs the method an <see cref="OptionsProviderAttribute"/>
///   names, for a configuration and an executable action alike.
/// </summary>
/// <remarks>
///   The same shapes are reported at the authoring site by the SHOKO0008
///   analyzer rule; the check here is for a plugin built without it.
/// </remarks>
internal static class UiOptionsProvider
{
    #region Validation

    private static readonly ConcurrentDictionary<Type, IReadOnlyDictionary<string, MethodInfo>> _providers = new();

    /// <summary>
    ///   Finds every options provider a type declares, and checks that each
    ///   lists values every member it names can take.
    /// </summary>
    /// <param name="owner">The type declaring the providers and their members.</param>
    /// <returns>The provider of each member that has one, by member name.</returns>
    /// <exception cref="NotSupportedException">
    ///   Thrown when a provider is not public, is generic, names a member the
    ///   type does not have, one that cannot take options or one another
    ///   provider claimed, names members of different option types, or returns
    ///   something else.
    /// </exception>
    public static IReadOnlyDictionary<string, MethodInfo> GetProviders(Type owner)
        => _providers.GetOrAdd(owner, CollectProviders);

    private static IReadOnlyDictionary<string, MethodInfo> CollectProviders(Type owner)
    {
        var providers = new Dictionary<string, MethodInfo>(StringComparer.Ordinal);
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
                if (GetOptionType(property.PropertyType, out var shapeFailure) is not { } memberOptionType)
                    throw Invalid(declaredBy, $"names \"{member}\", which {shapeFailure}");
                if (providers.TryGetValue(member, out var other))
                    throw Invalid(
                        declaredBy,
                        other == method ? $"names \"{member}\" twice" : $"names \"{member}\", which {other.Name} already provides for"
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

                providers[member] = method;
            }

            var returned = GetReturnedOptionType(method.ReturnType);
            if (returned != optionType && returned != typeof(SelectOption<>).TryMakeGenericType(optionType!))
            {
                var returnName = ShokoJsonSchemaGenerator.GetFriendlyTypeName(method.ReturnType);
                var optionName = ShokoJsonSchemaGenerator.GetFriendlyTypeName(optionType!);
                throw Invalid(declaredBy, $"returns {returnName} rather than a collection of {optionName}");
            }
        }

        return providers;
    }

    /// <summary>
    ///   The type of a single option for a member, with a collection offering
    ///   options for its entries and a nullable value for what it wraps.
    /// </summary>
    /// <param name="memberType">The member's type.</param>
    /// <param name="failure">Why the member takes no options, when it cannot.</param>
    /// <returns>The option type, or <c>null</c> when the member cannot take options.</returns>
    internal static Type? GetOptionType(Type memberType, out string? failure)
    {
        failure = null;
        var type = Nullable.GetUnderlyingType(memberType) ?? memberType;
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(SelectComponent<>))
        {
            failure = "is a select component, which carries its own options";
            return null;
        }

        if (type == typeof(string))
            return type;

        if (typeof(IDictionary).IsAssignableFrom(type) || FindGeneric(type, typeof(IDictionary<,>)) is not null ||
            FindGeneric(type, typeof(IReadOnlyDictionary<,>)) is not null)
        {
            failure = "is a dictionary, which has no single value to offer options for";
            return null;
        }

        var element = type.IsArray ? type.GetElementType()! : FindGeneric(type, typeof(IEnumerable<>))?.GetGenericArguments()[0];
        return element is null ? type : Nullable.GetUnderlyingType(element) ?? element;
    }

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

        return returnType.IsArray ? returnType.GetElementType() : FindGeneric(returnType, typeof(IEnumerable<>))?.GetGenericArguments()[0];
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
        => new($"The options provider {declaredBy} {failure}.");

    #endregion

    #region Lookup

    /// <summary>
    ///   Walks a path to the member it names, and returns the instance holding
    ///   it along with the method that member takes its options from.
    /// </summary>
    /// <param name="root">The configuration or action instance.</param>
    /// <param name="path">The member's path, as a custom action is invoked with.</param>
    /// <param name="isNewtonsoftJson">Whether the path uses the Newtonsoft member names.</param>
    /// <returns>The instance holding the member, and the method.</returns>
    /// <exception cref="ArgumentException">
    ///   Thrown when the path does not lead to a member that takes options.
    /// </exception>
    public static (object Owner, MethodInfo Method) Resolve(object root, string path, bool isNewtonsoftJson)
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
        if (!GetProviders(property.ReflectedType!).TryGetValue(property.Name, out var method))
            throw new ArgumentException($"The member at \"{path}\" takes no options", nameof(path));

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
    /// <returns>The options, in the order the method listed them.</returns>
    public static async Task<IReadOnlyList<UiOption>> InvokeAsync(
        MethodInfo method,
        IPluginManager pluginManager,
        object owner,
        IEnumerable<object?> arguments,
        Func<object?, JToken?> convert
    )
    {
        var result = method.Invoke<object>(pluginManager, owner, arguments);
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
        foreach (var value in values.Cast<object?>())
        {
            if (value is not null && value.GetType() is { IsGenericType: true } valueType && valueType.GetGenericTypeDefinition() == typeof(SelectOption<>))
            {
                options.Add(new()
                {
                    Value = convert(valueType.GetProperty(nameof(SelectOption<int>.Value))!.GetValue(value)),
                    Label = (string?)valueType.GetProperty(nameof(SelectOption<int>.Label))!.GetValue(value),
                });
                continue;
            }

            options.Add(new() { Value = convert(value) });
        }

        return options;
    }

    #endregion
}
