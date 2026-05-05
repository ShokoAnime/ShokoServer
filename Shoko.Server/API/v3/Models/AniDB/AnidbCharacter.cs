
using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Extensions;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Server;

#nullable enable
namespace Shoko.Server.API.v3.Models.AniDB;

public class AnidbCharacter
{
    /// <summary>
    /// The ID of the character.
    /// </summary>
    [Required]
    public int ID { get; set; }

    /// <summary>
    /// The name of the character.
    /// /// </summary>
    [Required]
    public string Name { get; set; }

    /// <summary>
    /// The original name of the character.
    /// </summary>
    public string? OriginalName { get; set; }

    /// <summary>
    /// The description of the character.
    /// </summary>
    [Required]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// The type of character.
    /// </summary>
    [Required, JsonConverter(typeof(StringEnumConverter))]
    public CharacterType Type { get; set; }

    /// <summary>
    /// The gender of the character.
    /// </summary>
    [Required]
    public string Gender { get; set; }

    /// <summary>
    /// The date that the character was last updated on AniDB.
    /// </summary>
    [Required]
    public DateTime LastUpdatedAt { get; set; }

    /// <summary>
    /// The image of the character, if available.
    /// </summary>
    public Image? Image { get; set; }

    public AnidbCharacter(AniDB_Character character)
    {
        ID = character.CharacterID;
        Name = character.Name;
        OriginalName = character.OriginalName;
        Description = character.Description;
        Type = character.Type;
        Gender = character.Gender.ToString();
        LastUpdatedAt = character.LastUpdated;
        Image = character.GetImageMetadata() is { } image ? new Image(image) : null;
    }
}
