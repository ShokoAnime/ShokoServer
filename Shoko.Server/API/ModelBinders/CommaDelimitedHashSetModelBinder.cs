using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;

#nullable enable
namespace Shoko.Server.API.ModelBinders;

public class CommaDelimitedHashSetModelBinder<T> : IModelBinder
{
    private readonly ILogger<CommaDelimitedHashSetModelBinder<T>> Logger;

    public CommaDelimitedHashSetModelBinder(ILogger<CommaDelimitedHashSetModelBinder<T>> logger)
    {
        Logger = logger;
    }

    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var valueProviderResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        var elementType = bindingContext.ModelType.GetElementType() ?? bindingContext.ModelType.GenericTypeArguments[0];
        var converter = TypeDescriptor.GetConverter(elementType);
        var result = valueProviderResult
            .SelectMany(value => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(value =>
            {
                try 
                {
                    return converter.ConvertFromString(value);
                }
                catch (FormatException e)
                {
                    Logger.LogDebug(e, $"Error converting value to {nameof(T)}.");
                    return null;
                }
            })
            .Where(t => t != null)
            .ToHashSet();
        bindingContext.Result = ModelBindingResult.Success(result);
        return Task.CompletedTask;
    }
}
