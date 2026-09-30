using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Shoko.Abstractions.Metadata;

namespace Shoko.Server.API.ModelBinders;

/// <summary>
///   Binds a <see cref="MetadataSource"/> from a route, query or form value,
///   taking the value or an alias of a registered source only, ignoring case.
///   Unlike the type's own converter, it never makes an unregistered source.
/// </summary>
public sealed class MetadataSourceModelBinder : IModelBinder
{
    /// <summary>
    ///   Binds the source, or adds a model error if no source is registered
    ///   under the text.
    /// </summary>
    /// <param name="bindingContext">The binding context.</param>
    /// <returns>A completed task.</returns>
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var valueProviderResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (valueProviderResult == ValueProviderResult.None)
            return Task.CompletedTask;

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, valueProviderResult);
        var text = valueProviderResult.FirstValue;
        if (string.IsNullOrWhiteSpace(text))
        {
            // Left for [Required] and the nullability checks to judge, as for any other empty value.
            bindingContext.Result = ModelBindingResult.Success(null);
            return Task.CompletedTask;
        }

        if (MetadataSource.TryGet(text, out var source))
            bindingContext.Result = ModelBindingResult.Success(source);
        else
            bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, $"\"{text}\" is not a registered metadata source.");
        return Task.CompletedTask;
    }
}

/// <summary>
///   Hands out <see cref="MetadataSourceModelBinder"/> for every
///   <see cref="MetadataSource"/> the API binds from a route, query or form
///   value. A body, a header or an explicit binder is left to its own
///   provider; a header value comes back through this one.
/// </summary>
public sealed class MetadataSourceModelBinderProvider : IModelBinderProvider
{
    private static readonly MetadataSourceModelBinder _binder = new();

    /// <summary>
    ///   Gets the binder for a model.
    /// </summary>
    /// <param name="context">The provider context.</param>
    /// <returns>
    ///   The binder for a <see cref="MetadataSource"/> bound from a value,
    ///   otherwise <c>null</c>.
    /// </returns>
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        if (context.Metadata.ModelType != typeof(MetadataSource) || context.BindingInfo.BinderType is not null)
            return null;

        var bindingSource = context.BindingInfo.BindingSource;
        return bindingSource is null || bindingSource == BindingSource.ModelBinding || bindingSource == BindingSource.Path
            || bindingSource == BindingSource.Query || bindingSource == BindingSource.Form
                ? _binder
                : null;
    }
}
