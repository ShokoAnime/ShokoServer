namespace Shoko.Server.API.v3.Models.Plugin.Input;

/// <summary>
///   Body for updating a package repository.
/// </summary>
public class UpdatePackageRepositoryBody
{
    /// <summary>
    ///   Enables or disables the repository. Left out, it stays as it is.
    /// </summary>
    public bool? IsEnabled { get; set; }
}
