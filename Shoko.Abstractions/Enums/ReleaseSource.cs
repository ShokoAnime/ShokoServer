
namespace Shoko.Abstractions.Enums;

/// <summary>
/// The source of the release.
/// </summary>
public enum ReleaseSource : byte
{
    /// <summary>
    /// Unknown.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Other. Catch-all for all other known types that aren't mapped yet.
    /// </summary>
    Other = 1,

    /// <summary>
    /// TV.
    /// </summary>
    TV = 2,

    /// <summary>
    /// DVD, be it normal DVD, HD DVD, or HF DVD.
    /// </summary>
    DVD = 3,

    /// <summary>
    /// Blu-ray. Be it normal Blu-ray, 4k Blu-ray, or any future revisions of
    /// the standard that are still considered to be Blu-ray.
    /// </summary>
    BluRay = 4,

    /// <summary>
    /// Web. Any and all web sources.
    /// </summary>
    Web = 5,

    /// <summary>
    /// VHS.
    /// </summary>
    VHS = 6,

    /// <summary>
    /// VCD, and SVCD.
    /// </summary>
    VCD = 7,

    /// <summary>
    /// Laser-disc.
    /// </summary>
    LaserDisc = 8,

    /// <summary>
    /// Filmed with a camera.
    /// </summary>
    Camera = 9,

    /// <summary>
    /// Digitized from a 8, 16 or 35mm film reel.
    /// </summary>
    Film = 10,
}
