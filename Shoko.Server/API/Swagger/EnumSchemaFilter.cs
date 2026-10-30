using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
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
        if (model is not OpenApiSchema mod) return;

        mod!.Enum!.Clear();
        mod.Type = JsonSchemaType.String;
        mod.Format = null;
        _names ??= GetWireNames();
        Array.ForEach(_names, name => model.Enum!.Add(name));
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
