using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Shoko.Plugin.Abstractions.Config;

namespace Shoko.Plugin.ReleaseExporter;

/// <summary>
/// Configure how the import/export functionality works.
/// </summary>
[Display(Name = "Release Importer/Exporter")]
public class ReleaseExporterConfiguration : IReleaseInfoProviderConfiguration
{
    /// <summary>
    /// Enables the exporter functionality.
    /// </summary>
    [Display(Name = "Export Releases")]
    [DefaultValue(false)]
    public bool IsExporterEnabled { get; set; } = false;

    /// <summary>
    /// Deletes the physical release file when a release is deleted from the
    /// system.
    /// </summary>
    [Display(Name = "Delete Physical Release Files")]
    [DefaultValue(false)]
    public bool DeletePhysicalReleaseFiles { get; set; } = false;

    /// <summary>
    /// The extension of the release file.
    /// </summary>
    [Required]
    [Display(Name = "Release Extension")]
    [RegularExpression(@"\.\b[a-zA-Z\.]{1,}\b")]
    [DefaultValue(".release.json")]
    public string ReleaseExtension { get; set; } = ".release.json";
}
