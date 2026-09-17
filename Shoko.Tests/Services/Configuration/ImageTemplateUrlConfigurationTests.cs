using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Settings;
using Xunit;

namespace Shoko.Tests.Services.Configuration;

/// <summary>
/// Unit tests covering <see cref="ImageTemplateUrlConfiguration.Validate"/>, which shares its template URL predicate
/// with <c>ImageManager.SetTemplateUrlForSource</c>. Both once rejected every URL because the scheme check was joined
/// with <c>||</c> instead of <c>&amp;&amp;</c>, and no scheme can be both http and https at the same time.
/// </summary>
public class ImageTemplateUrlConfigurationTests
{
    private static ImageTemplateUrlConfiguration Create(string? templateUrl)
        => new() { ImageSource = DataSource.TMDB, TemplateUrl = templateUrl };

    [Theory]
    [InlineData("http://example.com/images/{0}")]
    [InlineData("https://example.com/images/{0}")]
    [InlineData("https://image.tmdb.org/t/p/original/{0}")]
    public void Validate_AcceptsAbsoluteHttpAndHttpsTemplateUrls(string templateUrl)
    {
        var errors = ImageTemplateUrlConfiguration.Validate(Create(templateUrl));

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("ftp://example.com/images/{0}")]
    [InlineData("file:///var/images/{0}")]
    [InlineData("/images/{0}")]
    [InlineData("not a url at all {0}")]
    public void Validate_RejectsTemplateUrlsThatAreNotAbsoluteHttpOrHttps(string templateUrl)
    {
        var errors = ImageTemplateUrlConfiguration.Validate(Create(templateUrl));

        var urlErrors = Assert.Contains(nameof(ImageTemplateUrlConfiguration.TemplateUrl), errors);
        Assert.Contains(urlErrors, error => error.Contains("valid http:// or https:// URL"));
    }

    [Fact]
    public void Validate_RejectsTemplateUrlWithoutAPlaceholder()
    {
        var errors = ImageTemplateUrlConfiguration.Validate(Create("https://example.com/images/"));

        var urlErrors = Assert.Contains(nameof(ImageTemplateUrlConfiguration.TemplateUrl), errors);
        Assert.Contains(urlErrors, error => error.Contains("must contain {0}"));
    }

    [Fact]
    public void Validate_AcceptsANullTemplateUrl()
    {
        var errors = ImageTemplateUrlConfiguration.Validate(Create(null));

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_RejectsALocalImageSource()
    {
        var config = new ImageTemplateUrlConfiguration { ImageSource = DataSource.Shoko, TemplateUrl = "https://example.com/images/{0}" };

        var errors = ImageTemplateUrlConfiguration.Validate(config);

        Assert.Contains(nameof(ImageTemplateUrlConfiguration.ImageSource), errors);
    }
}
