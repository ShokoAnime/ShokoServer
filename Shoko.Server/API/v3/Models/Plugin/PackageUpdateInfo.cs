using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Models;

using AbstractPackageUpdateInfo = Shoko.Abstractions.Plugin.Models.PackageUpdateInfo;

#nullable enable
namespace Shoko.Server.API.v3.Models.Plugin;

/// <summary>
/// Describes an available update for a locally installed package. The full
/// package details are available through the other package endpoints.
/// </summary>
public class PackageUpdateInfo(AbstractPackageUpdateInfo updateInfo, IReadOnlyList<LocalPluginInfo> pluginInfoList, IPluginManager pluginManager)
{
    /// <summary>
    ///   The unique identifier of the package.
    /// </summary>
    [Required]
    public Guid PackageID { get; init; } = updateInfo.PackageID;

    /// <summary>
    ///   The display name of the package.
    /// </summary>
    [Required]
    public string Name { get; init; } = updateInfo.Name;

    /// <summary>
    ///   The currently installed version.
    /// </summary>
    [Required]
    public PackageInfo Current { get; init; } = new(updateInfo.Current, pluginInfoList, pluginManager);

    /// <summary>
    ///   The latest available version to update to.
    /// </summary>
    [Required]
    public PackageInfo Latest { get; init; } = new(updateInfo.Latest, pluginInfoList, pluginManager);
}
