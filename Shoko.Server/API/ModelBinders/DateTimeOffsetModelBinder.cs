using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Shoko.Server.API.ModelBinders;

/// <summary>
///   Binds a <see cref="DateTimeOffset"/> from an ISO 8601 date-time that
///   names its offset, <c>Z</c> or <c>±HH:MM</c>. A value without a time or
///   an offset is refused rather than read in the server's own time zone.
/// </summary>
public sealed partial class DateTimeOffsetModelBinder : IModelBinder
{
    /// <summary>
    ///   Binds the value, or adds a model error when it is not a date-time
    ///   with an offset.
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

        if (TryParse(text, out var value))
            bindingContext.Result = ModelBindingResult.Success(value);
        else
            bindingContext.ModelState.TryAddModelError(
                bindingContext.ModelName,
                $"\"{text}\" is not a date-time with an offset, e.g. 2026-10-04T00:00:00Z or 2026-10-04T00:00:00+09:00."
            );
        return Task.CompletedTask;
    }

    /// <summary>
    ///   Parse an ISO 8601 date-time that names its offset. An unencoded
    ///   <c>+</c> in a query string arrives as a space, so a space before the
    ///   offset is read as one.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="value">The parsed value.</param>
    /// <returns><c>true</c> when the text is a date-time with an offset.</returns>
    public static bool TryParse(string text, out DateTimeOffset value)
    {
        value = default;
        var match = DateTimeWithOffset().Match(text.Trim());
        if (!match.Success)
            return false;

        var normalized = match.Groups["offset"].Value is [' ', .. var rest] ? $"{match.Groups["dateTime"].Value}+{rest}" : match.Value;
        return DateTimeOffset.TryParse(normalized, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
    }

    private const string DateTimeWithOffsetPattern = @"^(?<dateTime>\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(?::\d{2}(?:\.\d{1,7})?)?)(?<offset>Z|[+\- ]\d{2}:\d{2})$";

    [GeneratedRegex(DateTimeWithOffsetPattern, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex DateTimeWithOffset();
}
