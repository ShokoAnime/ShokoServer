using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;

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

        // A value the element type does not know is the caller's mistake, so it fails the
        // binding (a 400 naming the value) rather than escaping as an exception.
        var values = new object?[items.Length];
        for (var i = 0; i < items.Length; i++)
        {
            try
            {
                values[i] = converter.ConvertFromString(items[i]);
            }
            catch (Exception e) when (e is FormatException or NotSupportedException or ArgumentException)
            {
                bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, $"\"{items[i]}\" is not a valid {elementType.Name}.");
            }
        }

        if (!bindingContext.ModelState.IsValid)
        {
            bindingContext.Result = ModelBindingResult.Failed();
            return Task.CompletedTask;
        }

        object? result;
        if (bindingContext.ModelType.IsArray)
        {
            var array = Array.CreateInstance(elementType, values.Length);
            Array.Copy(values, array, values.Length);
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

            foreach (var value in values)
                addMethod.Invoke(result, [value]);
        }

        bindingContext.Result = ModelBindingResult.Success(result);
        return Task.CompletedTask;
    }
}
