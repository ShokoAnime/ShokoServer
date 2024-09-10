using System;
using Shoko.Server.Providers.AniDB;

#nullable enable
namespace Shoko.Server.Models.AniDB;

public class AniDB_Creator
{
    #region DB Columns
    
    /// <summary>
    /// The local ID of the creator.
    /// </summary>
    public int AniDB_CreatorID { get; set; }

    
    /// <summary>
    /// The global ID of the creator.
    /// </summary>
    public int CreatorID { get; set; }

    /// <summary>
    /// The name of the creator, transcribed to use the latin alphabet.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The original name of the creator.
    /// </summary>
    public string? OriginalName { get; set; }

    /// <summary>
    /// The type of creator.
    /// </summary>
    public CreatorType Type { get; set; }

    /// <summary>
    /// The location of the image associated with the creator.
    /// </summary>
    public string? ImagePath { get; set; }

    /// <summary>
    /// The URL of the creator's English homepage.
    /// </summary>
    public string? EnglishHomepageUrl { get; set; }

    /// <summary>
    /// The URL of the creator's Japanese homepage.
    /// </summary>
    public string? JapaneseHomepageUrl { get; set; }

    /// <summary>
    /// The URL of the creator's English Wikipedia page.
    /// </summary>
    public string? EnglishWikiUrl { get; set; }

    /// <summary>
    /// The URL of the creator's Japanese Wikipedia page.
    /// </summary>
    public string? JapaneseWikiUrl { get; set; }

    /// <summary>
    /// The date that the creator was last updated on AniDB.
    /// </summary>
    public DateTime LastUpdatedAt { get; set; }

    #endregion
}
