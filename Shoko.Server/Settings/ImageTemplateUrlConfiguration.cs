using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Config.Enums;
using Shoko.Abstractions.Metadata.Enums;

#nullable enable
namespace Shoko.Server.Settings;

/// <summary>
///   Configuration for the image template URL.
/// </summary>
public class ImageTemplateUrlConfiguration
{
    /// <summary>
    ///   The image source.
    /// </summary>
    [Display(Name = "Image Source")]
    [Required]
    [DefaultValue(DataSource.AniDB)]
    [DeniedValues(DataSource.LocallyGenerated, DataSource.User, DataSource.None, DataSource.Shoko)]
    public DataSource ImageSource { get; set; } = DataSource.AniDB;

    [Key, Display(Name = "Image Template URL")]
    public string? TemplateUrl { get; set; }

    [ConfigurationAction(ConfigurationActionType.Validate)]
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Validate(ImageTemplateUrlConfiguration config)
    {
        var errors = new Dictionary<string, IReadOnlyList<string>>();
        if (config.ImageSource.IsLocal)
            errors.Add(nameof(config.ImageSource), [$"{nameof(config.ImageSource)} cannot be LocallyGenerated, User, None or Shoko."]);

        if (config.TemplateUrl is not null)
        {
            var urlErrors = new List<string>();
            if (!Uri.TryCreate(config.TemplateUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttp || uri.Scheme != Uri.UriSchemeHttps)
                urlErrors.Add($"{nameof(config.TemplateUrl)} must be a valid http:// or https:// URL.");
            if (!config.TemplateUrl.Contains("{0}"))
                urlErrors.Add($"{nameof(config.TemplateUrl)} must contain {{0}}.");
            if (urlErrors.Count > 0)
                errors.Add(nameof(config.TemplateUrl), urlErrors);
        }

        return errors;
    }
}
