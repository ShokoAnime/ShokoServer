using System.ComponentModel.DataAnnotations;
using System.Linq;
using Newtonsoft.Json;
using Shoko.Abstractions.Metadata;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Server;

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
    public Person Staff { get; set; } = null!;

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

    private const string CharacterRole = "Character";

    private Role() { }

    public Role(AniDB_Anime_Character xref, ICharacter character, ICreator? staff = null)
    {
        Character = character == null ? null : new()
        {
            ID = character.ID,
            Name = character.Name,
            AlternateName = character.OriginalName ?? string.Empty,
            Description = character.DefaultDescription?.Value ?? string.Empty,
            Image = character.PrimaryImage is { } characterImage ? new Image(characterImage) : null,
        };
        Staff = staff is not null
            ? new()
            {
                ID = staff.ID,
                Name = staff.Name,
                AlternateName = staff.OriginalName ?? string.Empty,
                Description = string.Empty,
                Image = staff.PrimaryImage is { } staffImage ? new Image(staffImage) : null,
                Type = staff.Type.ToString(),
            }
            : new()
            {
                ID = 0,
                Name = string.Empty,
                AlternateName = string.Empty,
                Description = string.Empty,
                Image = null,
                Type = "Unknown",
            };
        RoleName = CreatorRoleType.Actor;
        RoleDetails = staff is not null
            ? xref.AppearanceType.ToString().Replace("_", " ")
            : "Appears In";
    }

    public Role(AniDB_Anime_Staff xref, ICreator staff)
    {
        Staff = new()
        {
            ID = staff.ID,
            Name = staff.Name,
            AlternateName = staff.OriginalName ?? string.Empty,
            Description = string.Empty,
            Image = staff.PrimaryImage is { } staffImage ? new Image(staffImage) : null,
            Type = staff.Type.ToString(),
        };
        RoleName = xref.RoleType;
        RoleDetails = xref.Role;
    }

    public static Role? FromTmdb(TMDB_Cast cast)
    {
        var person = cast.GetTmdbPerson();
        if (person is null) return null;
        return new()
        {
            Character = new()
            {
                Name = cast.CharacterName,
            },
            Staff = CreateStaffFromTmdbPerson(person),
            RoleName = CreatorRoleType.Actor,
            RoleDetails = CharacterRole,
        };
    }

    public static Role? FromTmdb(TMDB_Crew crew)
    {
        var person = crew.GetTmdbPerson();
        if (person is null) return null;
        return new()
        {
            Staff = CreateStaffFromTmdbPerson(person),
            RoleName = crew.ToCreatorRole(),
            RoleDetails = $"{crew.Department}, {crew.Job}",
        };
    }

    private static Person CreateStaffFromTmdbPerson(TMDB_Person person)
    {
        return new()
        {
            ID = person.Id,
            Name = person.EnglishName,
            AlternateName = person.Aliases.Count == 0 ? person.EnglishName : person.Aliases[0].Split("/").Last().Trim(),
            Description = person.EnglishBiography,
            Image = (person as ICreator).PrimaryImage is { } staffImage ? new Image(staffImage) : null,
        };
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
        /// AniDB creator type, if the person object is an AniDB creator.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? Type { get; set; }

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
        [Required]
        public string AlternateName { get; set; } = string.Empty;

        /// <summary>
        /// A description, bio, etc
        /// ex. Sawano Hiroyuki was born September 12, 1980 in Tokyo, Japan. He is a composer and arranger.
        /// </summary>
        [Required]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// image object, usually a profile picture of sorts
        /// </summary>
        public Image? Image { get; set; }
    }
}
