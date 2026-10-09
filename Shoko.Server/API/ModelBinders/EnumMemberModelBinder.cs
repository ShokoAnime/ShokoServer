using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Shoko.Server.API.ModelBinders;

/// <summary>
///   Binds an enum from a route, query or form value by the
///   <see cref="EnumMemberAttribute"/> value it is written as, ignoring case,
///   and otherwise as the type converter would, by name or number.
/// </summary>
public sealed class EnumMemberModelBinder : IModelBinder
{
    private static readonly ConcurrentDictionary<Type, IReadOnlyDictionary<string, object>> _wireNames = new();

    /// <summary>
    ///   Binds the enum, or adds a model error if the text names no member.
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

        var enumType = Nullable.GetUnderlyingType(bindingContext.ModelType) ?? bindingContext.ModelType;
        if (GetWireNames(enumType).TryGetValue(text.Trim(), out var value) || Enum.TryParse(enumType, text, ignoreCase: true, out value))
            bindingContext.Result = ModelBindingResult.Success(value);
        else
            bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, $"\"{text}\" is not a valid {enumType.Name}.");
        return Task.CompletedTask;
    }

    /// <summary>
    ///   The members of an enum by the value each one's
    ///   <see cref="EnumMemberAttribute"/> gives, ignoring case.
    /// </summary>
    /// <param name="enumType">The enum type.</param>
    /// <returns>The members by their wire names; empty when none declares one.</returns>
    internal static IReadOnlyDictionary<string, object> GetWireNames(Type enumType)
        => _wireNames.GetOrAdd(
            enumType,
            type => type.GetFields(BindingFlags.Public | BindingFlags.Static)
                .Select(field => (Name: field.GetCustomAttribute<EnumMemberAttribute>()?.Value, Value: field.GetValue(null)!))
                .Where(x => !string.IsNullOrEmpty(x.Name))
                .DistinctBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Name!, x => x.Value, StringComparer.OrdinalIgnoreCase)
        );
}

/// <summary>
///   Hands out <see cref="EnumMemberModelBinder"/> for every enum declaring an
///   <see cref="EnumMemberAttribute"/> value the API binds from a route, query
///   or form value, so the name it is written as in a body binds too.
/// </summary>
public sealed class EnumMemberModelBinderProvider : IModelBinderProvider
{
    private static readonly EnumMemberModelBinder _binder = new();

    /// <summary>
    ///   Gets the binder for a model.
    /// </summary>
    /// <param name="context">The provider context.</param>
    /// <returns>
    ///   The binder for such an enum bound from a value, otherwise
    ///   <c>null</c>.
    /// </returns>
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        var enumType = Nullable.GetUnderlyingType(context.Metadata.ModelType) ?? context.Metadata.ModelType;
        if (!enumType.IsEnum || context.BindingInfo.BinderType is not null || EnumMemberModelBinder.GetWireNames(enumType).Count is 0)
            return null;

        var bindingSource = context.BindingInfo.BindingSource;
        return bindingSource is null || bindingSource == BindingSource.ModelBinding || bindingSource == BindingSource.Path
            || bindingSource == BindingSource.Query || bindingSource == BindingSource.Form
                ? _binder
                : null;
    }
}
