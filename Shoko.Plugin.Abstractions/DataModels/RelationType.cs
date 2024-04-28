
#nullable enable
namespace Shoko.Plugin.Abstractions.DataModels;

/// <summary>
/// Explains how the main entry relates to the related entry.
/// </summary>
public enum RelationType
{
    /// <summary>
    /// The relation between the entries cannot be explained in simple terms.
    /// </summary>
    Other = 0,

    /// <summary>
    /// The entries use the same setting, but follow different stories.
    /// </summary>
    SameSetting = 1,

    /// <summary>
    /// The entries use the same base story, but is set in alternate settings.
    /// </summary>
    AlternativeSetting = 2,

    /// <summary>
    /// The entries tell the same story in the same settings but are made at different times.
    /// </summary>
    AlternativeVersion = 3,

    /// <summary>
    /// The entries tell different stories in different settings but otherwise shares some character(s).
    /// </summary>
    SharedCharacters = 4,

    /// <summary>
    /// The first story either continues, or expands upon the story of the related entry.
    /// </summary>
    Prequel = 20,

    /// <summary>
    /// The related entry is the main-story for the main entry, which is a side-story.
    /// </summary>
    MainStory = 21,

    /// <summary>
    /// The related entry is a longer version of the summarized events in the main entry.
    /// </summary>
    FullStory = 22,

    /// <summary>
    /// The related entry either continues, or expands upon the story of the main entry.
    /// </summary>
    Sequel = 40,

    /// <summary>
    /// The related entry is a side-story for the main entry, which is the main-story.
    /// </summary>
    SideStory = 41,

    /// <summary>
    /// The related entry summarizes the events of the story in the main entry.
    /// </summary>
    Summary = 42,
}
