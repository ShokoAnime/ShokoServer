
namespace Shoko.Plugin.Abstractions.Enums;

/// <summary>
/// Types of creators.
/// </summary>
public enum CreatorType
{
    /// <summary>
    /// The creator's type is not known yet.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// A single person.
    /// </summary>
    Person = 1,

    /// <summary>
    /// A company or organization.
    /// </summary>
    Company = 2,

    /// <summary>
    /// A collaboration between two or more people
    /// under a common name. Mostly used for the
    /// source work of a show or movie when the
    /// source work is a manga under a shared pen
    /// name.
    /// </summary>
    Collaboration = 3,

    /// <summary>
    /// Misc. other type of creator.
    /// </summary>
    Other = 4,
}
