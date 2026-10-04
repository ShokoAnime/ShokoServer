using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Primitives;
using Shoko.Server.API.ModelBinders;
using Xunit;

namespace Shoko.Tests.API;

/// <summary>
/// Unit tests for <see cref="DateTimeOffsetModelBinder"/>, which binds the
/// airing routes' range to date-times that name their offset.
/// </summary>
public class DateTimeOffsetModelBinderTests
{
    private static async Task<DefaultModelBindingContext> Bind(string text)
    {
        var context = new DefaultModelBindingContext
        {
            ModelName = "from",
            ModelState = new ModelStateDictionary(),
            ValueProvider = new QueryStringValueProvider(
                BindingSource.Query,
                new QueryCollection(new Dictionary<string, StringValues> { ["from"] = text }),
                CultureInfo.InvariantCulture
            ),
        };

        await new DateTimeOffsetModelBinder().BindModelAsync(context);
        return context;
    }

    [Theory]
    [InlineData("2026-10-04T00:00:00Z", 0)]
    [InlineData("2026-10-04T09:00:00+09:00", 9)]
    [InlineData("2026-10-04T09:00+09:00", 9)]
    [InlineData("2026-10-03T19:00:00.000-05:00", -5)]
    [InlineData("2026-10-04T09:00:00 09:00", 9)]
    public async Task BindsADateTimeWithItsOffset(string text, int offsetHours)
    {
        var context = await Bind(text);

        var value = Assert.IsType<DateTimeOffset>(context.Result.Model);
        Assert.Equal(new DateTime(2026, 10, 4, 0, 0, 0, DateTimeKind.Utc), value.UtcDateTime);
        Assert.Equal(TimeSpan.FromHours(offsetHours), value.Offset);
        Assert.Equal(0, context.ModelState.ErrorCount);
    }

    [Theory]
    [InlineData("2026-10-04")]
    [InlineData("2026-10-04T00:00:00")]
    [InlineData("tomorrow")]
    public async Task RefusesAValueWithoutATimeOrAnOffset(string text)
    {
        var context = await Bind(text);

        Assert.False(context.Result.IsModelSet);
        Assert.Single(context.ModelState["from"]!.Errors);
    }
}
