using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Models.Server;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.Extensions;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Server;
using System.ComponentModel.DataAnnotations;
using System.Linq;

#nullable enable
namespace Shoko.Server.API.v3.Models.Common;

/// <summary>
/// This is for cast/staff
/// </summary>
public class Role
{
    /// <summary>
    /// The character played, if applicable
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public Person? Character { get; set; }

    /// <summary>
    /// The person who plays a character, writes the music, etc.
    /// </summary>
    [Required]
    public Person Staff { get; set; }

    /// <summary>
    /// The role that the staff plays, cv, writer, director, etc
    /// </summary>
    [Required]
    public CreatorRoleType RoleName { get; set; }

    /// <summary>
    /// Extra info about the role. For example, role can be voice actor, while role_details is Main Character
    /// </summary>
    [Required]
    public string RoleDetails { get; set; } = string.Empty;

    public Role(AniDB_Anime_Character xref, AniDB_Creator staff, AniDB_Character? character)
    {
        Character = character == null ? null : new()
        {
            ID = character.CharacterID,
            Name = character.Name,
            AlternateName = character.OriginalName ?? string.Empty,
            Description = character.Description ?? string.Empty,
            Image = character.GetImageMetadata() is { } characterImage ? new Image(characterImage) : null,
        };
        Staff = new()
        {
            ID = staff.CreatorID,
            Name = staff.Name,
            AlternateName = staff.OriginalName ?? string.Empty,
            Description = string.Empty,
            Image = staff.GetImageMetadata() is { } staffImage ? new Image(staffImage) : null,
        };
        RoleName = CreatorRoleType.Actor;
        RoleDetails = xref.AppearanceType.ToString().Replace("_", " ");
    }

    public Role(AniDB_Anime_Staff xref, AniDB_Creator staff)
    {
        Staff = new()
        {
            ID = staff.CreatorID,
            Name = staff.Name,
            AlternateName = staff.OriginalName ?? string.Empty,
            Description = string.Empty,
            Image = staff.GetImageMetadata() is { } staffImage ? new Image(staffImage) : null,
        };
        RoleName = xref.RoleType;
        RoleDetails = xref.Role;
    }

    public Role(TMDB_Movie_Cast cast)
    {
        var person = cast.GetTmdbPerson();
        var personImages = person.GetImages();
        Character = new()
        {
            Name = cast.CharacterName,
        };
        Staff = new()
        {
            ID = person.Id,
            Name = person.EnglishName,
            AlternateName = person.Aliases.Count == 0 ? person.EnglishName : person.Aliases[0].Split("/").Last().Trim(),
            Description = person.EnglishBiography,
            Image = personImages.Count > 0 ? new Image(personImages[0]) : null,
        };
        RoleName = CreatorRoleType.Actor;
        RoleDetails = "Character";
    }

    public Role(TMDB_Show_Cast cast)
    {
        var person = cast.GetTmdbPerson();
        var personImages = person.GetImages();
        Character = new()
        {
            Name = cast.CharacterName,
        };
        Staff = new()
        {
            ID = person.Id,
            Name = person.EnglishName,
            AlternateName = person.Aliases.Count == 0 ? person.EnglishName : person.Aliases[0].Split("/").Last().Trim(),
            Description = person.EnglishBiography,
            Image = personImages.Count > 0 ? new Image(personImages[0]) : null,
        };
        RoleName = CreatorRoleType.Actor;
        RoleDetails = "Character";
    }

    public Role(TMDB_Season_Cast cast)
    {
        var person = cast.GetTmdbPerson();
        var personImages = person.GetImages();
        Character = new()
        {
            Name = cast.CharacterName,
        };
        Staff = new()
        {
            ID = person.Id,
            Name = person.EnglishName,
            AlternateName = person.Aliases.Count == 0 ? person.EnglishName : person.Aliases[0].Split("/").Last().Trim(),
            Description = person.EnglishBiography,
            Image = personImages.Count > 0 ? new Image(personImages[0]) : null,
        };
        RoleName = CreatorRoleType.Actor;
        RoleDetails = "Character";
    }

    public Role(TMDB_Episode_Cast cast)
    {
        var person = cast.GetTmdbPerson();
        var personImages = person.GetImages();
        Character = new()
        {
            Name = cast.CharacterName,
        };
        Staff = new()
        {
            ID = person.Id,
            Name = person.EnglishName,
            AlternateName = person.Aliases.Count == 0 ? person.EnglishName : person.Aliases[0].Split("/").Last().Trim(),
            Description = person.EnglishBiography,
            Image = personImages.Count > 0 ? new Image(personImages[0]) : null,
        };
        RoleName = CreatorRoleType.Actor;
        RoleDetails = "Character";
    }

    public Role(TMDB_Movie_Crew crew)
    {
        var person = crew.GetTmdbPerson();
        var personImages = person.GetImages();
        Staff = new()
        {
            Name = person.EnglishName,
            AlternateName = person.Aliases.Count == 0 ? person.EnglishName : person.Aliases[0].Split("/").Last().Trim(),
            Description = person.EnglishBiography,
            Image = personImages.Count > 0 ? new Image(personImages[0]) : null,
        };
        RoleName = crew.ToCreatorRole();
        RoleDetails = $"{crew.Department}, {crew.Job}";
    }

    public Role(TMDB_Show_Crew crew)
    {
        var person = crew.GetTmdbPerson();
        var personImages = person.GetImages();
        Staff = new()
        {
            ID = person.Id,
            Name = person.EnglishName,
            AlternateName = person.Aliases.Count == 0 ? person.EnglishName : person.Aliases[0].Split("/").Last().Trim(),
            Description = person.EnglishBiography,
            Image = personImages.Count > 0 ? new Image(personImages[0]) : null,
        };
        RoleName = crew.ToCreatorRole();
        RoleDetails = $"{crew.Department}, {crew.Job}";
    }

    public Role(TMDB_Season_Crew crew)
    {
        var person = crew.GetTmdbPerson();
        var personImages = person.GetImages();
        Staff = new()
        {
            ID = person.Id,
            Name = person.EnglishName,
            AlternateName = person.Aliases.Count == 0 ? person.EnglishName : person.Aliases[0].Split("/").Last().Trim(),
            Description = person.EnglishBiography,
            Image = personImages.Count > 0 ? new Image(personImages[0]) : null,
        };
        RoleName = crew.ToCreatorRole();
        RoleDetails = $"{crew.Department}, {crew.Job}";
    }

    public Role(TMDB_Episode_Crew crew)
    {
        var person = crew.GetTmdbPerson();
        var personImages = person.GetImages();
        Staff = new()
        {
            ID = person.Id,
            Name = person.EnglishName,
            AlternateName = person.Aliases.Count == 0 ? person.EnglishName : person.Aliases[0].Split("/").Last().Trim(),
            Description = person.EnglishBiography,
            Image = personImages.Count > 0 ? new Image(personImages[0]) : null,
        };
        RoleName = crew.ToCreatorRole();
        RoleDetails = $"{crew.Department}, {crew.Job}";
    }

    /// <summary>
    /// A generic person object with the name, altname, description, and image
    /// </summary>
    public class Person
    {
        /// <summary>
        /// The provider id of the person object, if available and applicable.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? ID { get; set; }

        /// <summary>
        /// Main Name, romanized if needed
        /// ex. Sawano Hiroyuki
        /// </summary>
        [Required]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Alternate Name, this can be any other name, whether kanji, an alias, etc
        /// ex. 澤野弘之
        /// </summary>
        public string AlternateName { get; set; } = string.Empty;

        /// <summary>
        /// A description, bio, etc
        /// ex. Sawano Hiroyuki was born September 12, 1980 in Tokyo, Japan. He is a composer and arranger.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// image object, usually a profile picture of sorts
        /// </summary>
        public Image? Image { get; set; }
    }
}
