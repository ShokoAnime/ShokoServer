using System;
using System.ComponentModel.DataAnnotations;

using AbstractPackageInfo = Shoko.Abstractions.Plugin.Models.PackageInfo;
using AbstractPackageInstallQueueEntry = Shoko.Abstractions.Plugin.Models.PackageInstallQueueEntry;

namespace Shoko.Server.API.v3.Models.Plugin;

/// <summary>
///   A package install in the install queue.
/// </summary>
/// <param name="entry">The queue entry.</param>
public class PackageInstallQueueEntry(AbstractPackageInstallQueueEntry entry)
{
    /// <summary>
    ///   The entry's ID, the same for the same plan. Cancel the install with
    ///   it, and match the <c>plugin</c> feed's install events by it.
    /// </summary>
    [Required]
    public Guid ID { get; init; } = entry.ID;

    /// <summary>
    ///   The package asked for.
    /// </summary>
    [Required]
    public PackageReference Package { get; init; } = new(entry.Package);

    /// <summary>
    ///   The package being installed right now, the one asked for or one of
    ///   its dependencies, or <c>null</c> while the entry waits.
    /// </summary>
    public PackageReference? CurrentPackage { get; init; } = entry.CurrentPackage is { } current ? new(current) : null;

    /// <summary>
    ///   When the install was first asked for.
    /// </summary>
    [Required]
    public DateTime QueuedAt { get; init; } = entry.QueuedAt;

    /// <summary>
    ///   When the install started, or <c>null</c> while it waits.
    /// </summary>
    public DateTime? StartedAt { get; init; } = entry.StartedAt;

    /// <summary>
    ///   How many callers are waiting for this install right now.
    /// </summary>
    [Required]
    public int Waiting { get; init; } = entry.Waiting;

    /// <summary>
    ///   A package release, by name, version and runtime.
    /// </summary>
    /// <param name="package">The package.</param>
    public class PackageReference(AbstractPackageInfo package)
    {
        /// <summary>
        ///   The package's ID.
        /// </summary>
        [Required]
        public Guid PackageID { get; init; } = package.Manifest.PackageID;

        /// <summary>
        ///   The package's name.
        /// </summary>
        [Required]
        public string Name { get; init; } = package.Manifest.Name;

        /// <summary>
        ///   The release's version.
        /// </summary>
        [Required]
        public Version Version { get; init; } = package.Release.Version;

        /// <summary>
        ///   The archive's runtime identifier.
        /// </summary>
        [Required]
        public string RuntimeIdentifier { get; init; } = package.Archive.RuntimeIdentifier;
    }
}
