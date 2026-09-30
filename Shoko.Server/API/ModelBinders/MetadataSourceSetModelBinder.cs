using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Shoko.Abstractions.Metadata;
using Shoko.Server.API.v3.Models.Common;

#pragma warning disable CS0618
namespace Shoko.Server.API.ModelBinders;

/// <summary>
///   Binds a set of <see cref="MetadataSource"/>s from a query value, such as
///   <c>includeDataFrom</c>, given comma-separated, repeated or both.
/// </summary>
/// <remarks>
///   Each item is read first as the old <see cref="DataSourceType"/> it may
///   have been sent as, name or number, so every value the parameter took
///   before still means the same source; then as the value, an alias or an
///   old spelling of a registered source, ignoring case. An item naming no
///   registered source is left out, as the old enum's binder left out a
///   name it did not know, so a request is never refused over it.
/// </remarks>
public sealed class MetadataSourceSetModelBinder : IModelBinder
{
    /// <summary>
    ///   Binds the set, or leaves the model unbound when the value is absent.
    /// </summary>
    /// <param name="bindingContext">The binding context.</param>
    /// <returns>A completed task.</returns>
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var valueProviderResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (valueProviderResult == ValueProviderResult.None)
            return Task.CompletedTask;

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, valueProviderResult);
        bindingContext.Result = ModelBindingResult.Success(Parse(valueProviderResult));
        return Task.CompletedTask;
    }

    /// <summary>
    ///   Reads the sources out of the values, each of which may hold several
    ///   separated by commas.
    /// </summary>
    /// <param name="values">The values as sent.</param>
    /// <returns>The sources named, once each.</returns>
    internal static HashSet<MetadataSource> Parse(IEnumerable<string?> values)
        => values
            .SelectMany(value => (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(ParseOne)
            .OfType<MetadataSource>()
            .ToHashSet();

    /// <summary>
    ///   Reads one source, the old enum's way first.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The source, or <c>null</c> when no registered source is named.</returns>
    internal static MetadataSource? ParseOne(string text)
    {
        if (Enum.TryParse<DataSourceType>(text, ignoreCase: true, out var dataSource) && Enum.IsDefined(dataSource))
        {
            var value = dataSource switch
            {
                DataSourceType.AniDB => MetadataSource.AniDB.Value,
                DataSourceType.TMDB => MetadataSource.TMDB.Value,
                _ => dataSource.ToString(),
            };
            return MetadataSource.TryGet(value, out var old) ? old : null;
        }

        return MetadataSource.TryGet(text, out var source) ? source : null;
    }
}
