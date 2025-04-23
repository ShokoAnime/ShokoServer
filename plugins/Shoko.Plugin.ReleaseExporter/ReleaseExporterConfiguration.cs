using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Config;
using Shoko.Plugin.Abstractions.Config.Attributes;
using Shoko.Plugin.Abstractions.Config.Enums;
using Shoko.Plugin.Abstractions.DataModels;

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
    /// Enables the relocation of release file(s) when a video file is
    /// relocated.
    /// </summary>
    [Display(Name = "Relocate Releases")]
    [DefaultValue(true)]
    public bool IsRelocationEnabled { get; set; } = true;

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

    /// <summary>
    /// The templates to resolve the locations of the release file for a video
    /// file.
    /// <br/>
    /// You can use the following template tags;
    /// <br/>
    /// - <c>%data_root%</c> to get the user data folder with a trailing slash,
    /// <br/>
    /// - <c>%managed_folder_root%</c> to get the managed folder's root with a trailing
    ///   slash,
    /// <br/>
    /// - <c>%managed_folder_id%</c> to get the managed folder's ID,
    /// <br/>
    /// - <c>%managed_folder_name%</c> to get the managed folder's name,
    /// <br/>
    /// - <c>%file_path%</c> to get the relative path of the file within the
    ///   managed folder without the extension without a leading slash,
    /// <br/>
    /// - <c>%file_name%</c> to get the name of the file without the extension,
    /// <br/>
    /// - <c>%file_extension%</c> to get the extension of the file,
    /// <br/>
    /// - <c>%ed2k_hash%</c> to get the ed2k hash of the file,
    /// <br/>
    /// - <c>%file_size%</c> to get the size of the file in bytes, and
    /// <br/>
    /// - <c>%release_extension%</c> to get the release extension.
    /// </summary>
    [Badge("Advanced", Theme = DisplayColorTheme.Primary)]
    [Visibility(Size = DisplayElementSize.Small, Advanced = true)]
    [TextArea]
    [Display(Name = "Release Location Templates")]
    [RegularExpression(@"\.\b[a-zA-Z\.]{1,}\b")]
    [DefaultValue(new string[] { "%managed_root%%file_path%%release_extension%" })]
    public List<string> ReleaseLocationTemplates { get; set; } = ["%managed_root%%file_path%%release_extension%"];

    /// <summary>
    /// Get the release file path for a video file.
    /// </summary>
    /// <param name="applicationPaths">The application paths.</param>
    /// <param name="managedFolder">The managed folder.</param>
    /// <param name="video">The video.</param>
    /// <param name="relativePath">The relative path.</param>
    /// <returns>The release file path, or null if the template is null or empty.</returns>
    public IEnumerable<string> GetReleaseFilePaths(IApplicationPaths applicationPaths, IManagedFolder managedFolder, IVideo video, string relativePath)
    {
        foreach (var tem in ReleaseLocationTemplates)
        {
            var template = tem;
            if (string.IsNullOrEmpty(template))
                continue;

            if (template.Contains("%data_root%"))
                template = template.Replace("%data_root%", applicationPaths.ProgramDataPath + Path.DirectorySeparatorChar);

            if (template.Contains("%managed_folder_root%"))
                template = template.Replace("%managed_folder_root%", managedFolder.Path);

            if (template.Contains("%managed_folder_id%"))
                template = template.Replace("%managed_folder_id%", managedFolder.ID.ToString());

            if (template.Contains("%managed_folder_name%"))
                template = template.Replace("%managed_folder_name%", managedFolder.Name.RemoveInvalidPathCharacters());

            if (template.Contains("%file_path%"))
                template = template.Replace("%file_path%", relativePath[1..Path.GetExtension(relativePath).Length]);

            if (template.Contains("%file_name%"))
                template = template.Replace("%file_name%", Path.GetFileNameWithoutExtension(relativePath));

            if (template.Contains("%file_extension%"))
                template = template.Replace("%file_extension%", Path.GetExtension(relativePath));

            if (template.Contains("%ed2k_hash%"))
                template = template.Replace("%ed2k_hash%", video.ED2K);

            if (template.Contains("%file_size%"))
                template = template.Replace("%file_size%", video.Size.ToString());

            if (template.Contains("%release_extension%"))
                template = template.Replace("%release_extension%", ReleaseExtension);

            yield return template.Replace('/', Path.DirectorySeparatorChar);
        }
    }
}
