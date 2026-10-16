using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Shoko.Abstractions.Metadata;
using Shoko.Server.Databases;

namespace Shoko.Server.API.ModelBinders;

/// <summary>
///   Binds a <see cref="MetadataEntityType"/> from a route, query or form
///   value, taking the value or an alias of a registered entity type, or the
///   value of one rows are stored under, ignoring case. Unlike the type's own
///   converter, it never makes up an entity type nothing knows.
/// </summary>
public sealed class MetadataEntityTypeModelBinder : IModelBinder
{
    /// <summary>
    ///   Binds the entity type, or adds a model error if no entity type is
    ///   registered or stored under the text.
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

        if (MetadataNumberRegistry.TryGetKnownEntityType(text, out var entityType))
            bindingContext.Result = ModelBindingResult.Success(entityType);
        else
            bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, $"\"{text}\" is not a known metadata entity type.");
        return Task.CompletedTask;
    }
}

/// <summary>
///   Hands out <see cref="MetadataEntityTypeModelBinder"/> for every
///   <see cref="MetadataEntityType"/> the API binds from a route, query or
///   form value. A body, a header or an explicit binder is left to its own
///   provider.
/// </summary>
public sealed class MetadataEntityTypeModelBinderProvider : IModelBinderProvider
{
    private static readonly MetadataEntityTypeModelBinder _binder = new();

    /// <summary>
    ///   Gets the binder for a model.
    /// </summary>
    /// <param name="context">The provider context.</param>
    /// <returns>
    ///   The binder for a <see cref="MetadataEntityType"/> bound from a value,
    ///   otherwise <c>null</c>.
    /// </returns>
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        if (context.Metadata.ModelType != typeof(MetadataEntityType) || context.BindingInfo.BinderType is not null)
            return null;

        var bindingSource = context.BindingInfo.BindingSource;
        return bindingSource is null || bindingSource == BindingSource.ModelBinding || bindingSource == BindingSource.Path
            || bindingSource == BindingSource.Query || bindingSource == BindingSource.Form
                ? _binder
                : null;
    }
}
