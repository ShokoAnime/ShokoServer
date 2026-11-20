using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Shoko.Server.API.Swagger;

/// <summary>
///   Describes an enum as the strings the API sends for it: a member's
///   <see cref="EnumMemberAttribute.Value"/> where it has one, as
///   Newtonsoft's <c>StringEnumConverter</c> writes it, else its name.
/// </summary>
public class EnumSchemaFilter<T> : ISchemaFilter where T : struct, Enum
{
    private string[]? _names;

    public void Apply(IOpenApiSchema model, SchemaFilterContext context)
    {
        if (!context.Type.IsEnum) return;
        if (context.Type != typeof(T)) return;
        // A property or parameter of the enum's type is an allOf over the
        // enum's own schema; only its default is the enum's to name.
        if (model is OpenApiSchema { Enum: null, Default: { } value } wrapper)
        {
            if (GetWireName(value) is { } wireName)
                wrapper.Default = JsonValue.Create(wireName);
            return;
        }

        if (model is not OpenApiSchema { Enum: not null } mod) return;

        mod.Enum.Clear();
        mod.Type = JsonSchemaType.String;
        mod.Format = null;
        _names ??= GetWireNames();
        Array.ForEach(_names, name => model.Enum!.Add(name));
    }

    /// <summary>
    ///   The string the API sends for a default written as the member's
    ///   number or in another casing of its name.
    /// </summary>
    /// <param name="value">The default as written.</param>
    /// <returns>The wire name, or <c>null</c> when no single member has the value.</returns>
    internal static string? GetWireName(JsonNode value)
    {
        T member;
        if (value is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text))
        {
            if (!Enum.TryParse(text, ignoreCase: true, out member))
                return GetWireNames().FirstOrDefault(name => string.Equals(name, text, StringComparison.OrdinalIgnoreCase));
        }
        else if (value is JsonValue number && TryGetNumber(number, out var raw))
        {
            member = (T)Enum.ToObject(typeof(T), raw);
        }
        else
        {
            return null;
        }

        var field = typeof(T).GetField(member.ToString(), BindingFlags.Public | BindingFlags.Static);
        return field is null ? null : field.GetCustomAttribute<EnumMemberAttribute>()?.Value ?? field.Name;
    }

    private static bool TryGetNumber(JsonValue value, out long number)
    {
        if (value.TryGetValue(out number))
            return true;
        if (!value.TryGetValue<int>(out var small))
            return false;
        number = small;
        return true;
    }

    /// <summary>
    ///   The strings the API sends for the enum's members, in their declared
    ///   order.
    /// </summary>
    internal static string[] GetWireNames()
        => typeof(T).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(field => field.GetCustomAttribute<EnumMemberAttribute>()?.Value ?? field.Name)
            .ToArray();
}
