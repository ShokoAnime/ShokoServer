using Shoko.Abstractions.Video.Enums;

namespace Shoko.Abstractions.Metadata;

/// <summary>
/// Represents the count of files by their source type.
/// </summary>
public class FileSourceCounts
{
    /// <summary>
    /// Unknown source.
    /// </summary>
    public int Unknown { get; set; }

    /// <summary>
    /// Other known source that isn't mapped yet.
    /// </summary>
    public int Other { get; set; }

    /// <summary>
    /// TV.
    /// </summary>
    public int TV { get; set; }

    /// <summary>
    /// DVD, HD DVD, or HF DVD.
    /// </summary>
    public int DVD { get; set; }

    /// <summary>
    /// Blu-ray, 4k Blu-ray, or any future revisions.
    /// </summary>
    public int BluRay { get; set; }

    /// <summary>
    /// Web sources.
    /// </summary>
    public int Web { get; set; }

    /// <summary>
    /// VHS.
    /// </summary>
    public int VHS { get; set; }

    /// <summary>
    /// VCD or SVCD.
    /// </summary>
    public int VCD { get; set; }

    /// <summary>
    /// Laser-disc.
    /// </summary>
    public int LaserDisc { get; set; }

    /// <summary>
    /// Filmed with a camera.
    /// </summary>
    public int Camera { get; set; }

    /// <summary>
    /// Digitized from a film reel.
    /// </summary>
    public int Film { get; set; }

    /// <summary>
    /// Returns the number of files for the given <paramref name="source"/>.
    /// </summary>
    public int this[ReleaseSource source] => source switch
    {
        ReleaseSource.Unknown => Unknown,
        ReleaseSource.Other => Other,
        ReleaseSource.TV => TV,
        ReleaseSource.DVD => DVD,
        ReleaseSource.BluRay => BluRay,
        ReleaseSource.Web => Web,
        ReleaseSource.VHS => VHS,
        ReleaseSource.VCD => VCD,
        ReleaseSource.LaserDisc => LaserDisc,
        ReleaseSource.Camera => Camera,
        ReleaseSource.Film => Film,
        _ => Other
    };
}
