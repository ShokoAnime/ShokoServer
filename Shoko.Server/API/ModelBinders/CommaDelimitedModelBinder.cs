using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;

#nullable enable
namespace Shoko.Server.API.ModelBinders;

public class CommaDelimitedModelBinder : IModelBinder
{
    private readonly ILogger<CommaDelimitedModelBinder> _logger;
    private static readonly Dictionary<Type, MethodInfo> AddCache = new();

    public CommaDelimitedModelBinder(ILogger<CommaDelimitedModelBinder> logger)
    {
        _logger = logger;
    }

    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var valueProviderResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (valueProviderResult == ValueProviderResult.None) return Task.CompletedTask;
        var elementType = bindingContext.ModelType.GetElementType() ?? bindingContext.ModelType.GenericTypeArguments[0];
        var converter = TypeDescriptor.GetConverter(elementType);

        var items = valueProviderResult
            .SelectMany(value => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).ToArray();
        object? result;

        if (bindingContext.ModelType.IsArray)
        {
            var array = Array.CreateInstance(elementType, items.Length);
            Array.Copy(items.Select(a => converter.ConvertFromString(a)).ToArray(), array, items.Length);
            result = array;
        }
        else
        {
            // HashSet<T> makes things really hard, as it needs compile time types
            result = Activator.CreateInstance(bindingContext.ModelType);

            MethodInfo? addMethod;
            lock (AddCache)
            {
                if (!AddCache.TryGetValue(bindingContext.ModelType, out addMethod))
                {
                    addMethod = bindingContext.ModelType.GetMethod("Add");
                    if (addMethod != null) AddCache[bindingContext.ModelType] = addMethod;
                }
            }

            if (addMethod == null)
            {
                _logger.LogDebug("Could not get Add method for {Type}", bindingContext.ModelType.FullName);
                return Task.CompletedTask;
            }

            foreach (var item in items)
            {
                try
                {
                    var value = converter.ConvertFromString(item);
                    addMethod.Invoke(result, [value]);
                }
                catch (Exception e)
                {
                    _logger.LogDebug(e, "Error converting value to {Name}", elementType.FullName);
                }
            }
        }

        bindingContext.Result = ModelBindingResult.Success(result);
        return Task.CompletedTask;
    }
}
