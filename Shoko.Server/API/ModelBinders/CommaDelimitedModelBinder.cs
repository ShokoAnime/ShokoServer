using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;

#nullable enable
namespace Shoko.Server.API.ModelBinders;

public class CommaDelimitedModelBinder : IModelBinder
{
    private readonly ILogger<CommaDelimitedModelBinder> _logger;

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

        // HashSet<T> makes things really hard, as it needs compile time types
        var result = Activator.CreateInstance(bindingContext.ModelType);
        var addMethod = result?.GetType().GetMethod("Add");
        if (addMethod == null)
        {
            _logger.LogDebug("Could not get Add method for {Type}", bindingContext.ModelType.FullName);
            return Task.CompletedTask;
        }

        foreach (var item in valueProviderResult
                     .SelectMany(value => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
        {
            try
            {
                var value = converter.ConvertFromString(item);
                addMethod.Invoke(result, new[] { value });
            }
            catch (Exception e)
            {
                _logger.LogDebug(e, "Error converting value to {Name}", elementType.FullName);
            }
        }

        bindingContext.Result = ModelBindingResult.Success(result);
        return Task.CompletedTask;
    }
}
