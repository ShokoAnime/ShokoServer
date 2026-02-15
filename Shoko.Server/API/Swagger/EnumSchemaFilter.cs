using System;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

#nullable enable
namespace Shoko.Server.API.Swagger;

public class EnumSchemaFilter<T> : ISchemaFilter where T : struct, Enum
{
    private string[]? _names;

    public void Apply(OpenApiSchema model, SchemaFilterContext context)
    {
        if (!context.Type.IsEnum) return;
        if (context.Type != typeof(T)) return;

        model.Enum.Clear();
        model.Type = "string";
        model.Format = null;
        _names ??= Enum.GetNames<T>();
        Array.ForEach(_names, name => model.Enum.Add(new OpenApiString(name)));
    }
}
