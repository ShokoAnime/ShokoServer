using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Shoko.Plugin.Abstractions.Config;
using Shoko.Plugin.Abstractions.Config.Attributes;

namespace Shoko.Plugin.ReleaseExporter;

/// <summary>
/// Configure how the export functionality works.
/// </summary>
[DisplaySections(["General", "File Settings"])]
public class ReleaseExporterConfiguration : IConfiguration
{
    /// <summary>
    /// Enables the exporter functionality.
    /// </summary>
    [DisplaySection("General")]
    public bool IsExporterEnabled { get; set; }

    /// <summary>
    /// The extension of the release file
    /// </summary>
    [Required]
    [Display(Name = "Release Extension")]
    [RegularExpression(@"\.\b[a-zA-Z\.]{1,}\b")]
    [DefaultValue(".release.json")]
    [DisplaySection("File Settings")]
    public string ReleaseExtension { get; set; } = ".release.json";
}
