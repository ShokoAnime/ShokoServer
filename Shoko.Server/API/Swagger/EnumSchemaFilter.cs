using System;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

#nullable enable
namespace Shoko.Server.API.Swagger;

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
        _names ??= Enum.GetNames<T>();
        Array.ForEach(_names, name => model.Enum!.Add(name));
    }
}
