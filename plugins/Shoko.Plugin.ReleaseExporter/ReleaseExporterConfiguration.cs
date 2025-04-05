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
    public bool IsExporterEnabled { get; set; }

    /// <summary>
    /// Deletes the physical release file when a release is deleted from the
    /// system.
    /// </summary>
    [Display(Name = "Also Delete Physical Release Files")]
    [DefaultValue(true)]
    public bool DeletePhysicalReleaseFiles { get; set; } = true;

    /// <summary>
    /// The extension of the release file.
    /// </summary>
    [Required]
    [Display(Name = "Release Extension")]
    [RegularExpression(@"\.\b[a-zA-Z\.]{1,}\b")]
    [DefaultValue(".release.json")]
    public string ReleaseExtension { get; set; } = ".release.json";
}
