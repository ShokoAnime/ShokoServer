using System;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Services;
using Xunit;

namespace Shoko.IntegrationTests;

/// <summary>
/// Checks the template URLs of a plugin's image source on the running server:
/// the registered default, the user's own over it, and going back to the
/// default when the user's is cleared.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ImageTemplateRegistrationTests(DatabaseMigrationFixture fixture)
{
    private const string Template = "https://images.example.com/{0}";

    [Fact]
    public void ARegisteredTemplateIsTheDefaultUnderTheUsersOwn()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var source = TestSources.Template;
        var images = fixture.Services.GetRequiredService<IImageManager>();
        Assert.Null(images.GetTemplateUrlForSource(source));

        images.RegisterTemplateUrl(source, Template);
        Assert.Equal(Template, images.GetTemplateUrlForSource(source));
        Assert.Equal(Template, images.GetTemplateUrls()[source]);

        images.SetTemplateUrlForSource(source, "https://mirror.example.com/{0}");
        Assert.Equal("https://mirror.example.com/{0}", images.GetTemplateUrlForSource(source));

        images.SetTemplateUrlForSource(source, null);
        Assert.Equal(Template, images.GetTemplateUrlForSource(source));

        Assert.Throws<InvalidOperationException>(() => images.RegisterTemplateUrl(MetadataSource.TMDB, Template));
        Assert.Throws<ArgumentException>(() => images.RegisterTemplateUrl(source, "https://images.example.com/"));
    }
}
