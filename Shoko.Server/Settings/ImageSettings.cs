using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Config.Enums;
using Shoko.Abstractions.Metadata;

namespace Shoko.Server.Settings;

public class ImageSettings
{
    /// <summary>
    ///   List of user-registered image template URLs.
    /// </summary>
    [List(ListType = DisplayListType.ComplexInline)]
    public List<ImageTemplateUrlConfiguration> ImageTemplateUrls { get; set; } = [];

    /// <summary>
    ///   Which images to download for a metadata source that has no
    ///   settings of its own in <see cref="MetadataSources"/>.
    /// </summary>
    [Display(Name = "Metadata Source Image Defaults")]
    public MetadataImageSettings MetadataSourceDefaults { get; set; } = new();

    /// <summary>
    ///   Which images to download for TMDB and each plugin metadata source, keyed by
    ///   source, in place of <see cref="MetadataSourceDefaults"/>.
    /// </summary>
    [Display(Name = "Metadata Source Images")]
    [List(ListType = DisplayListType.ComplexTab)]
    public List<MetadataSourceImageSettings> MetadataSources { get; set; } = [];

    /// <summary>
    ///   The image settings in effect for a metadata source: its own,
    ///   else the shared defaults.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>The settings.</returns>
    public MetadataImageSettings GetMetadataSourceSettings(MetadataSource source)
        => MetadataSources.FirstOrDefault(settings => settings.Source == source) ?? MetadataSourceDefaults;

    /// <summary>
    ///   Automatically purge orphaned images on a periodic schedule (every 24 hours).
    ///   Orphaned images are no longer referenced by any entity in the database.
    /// </summary>
    [Display(Name = "Auto Purge Orphaned Images")]
    public bool AutoPurge { get; set; } = true;

    /// <summary>
    ///   Automatically validate the integrity of all available images on a periodic schedule
    ///   (every 24 hours). Invalid images will be re-downloaded.
    /// </summary>
    [Display(Name = "Auto Validate Image Integrity")]
    public bool AutoValidate { get; set; } = false;
}
