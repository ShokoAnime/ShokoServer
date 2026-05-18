using System.Collections.Generic;
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
}
