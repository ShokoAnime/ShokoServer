using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Config.Enums;

#nullable enable
namespace Shoko.Server.Settings;

public class ImageSettings
{
    /// <summary>
    ///   List of user-registered image template URLs.
    /// </summary>
    [List(ListType = DisplayListType.ComplexInline)]
    public List<ImageTemplateUrlConfiguration> ImageTemplateUrls { get; set; } = [];

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
