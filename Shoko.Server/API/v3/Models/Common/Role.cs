using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Metadata;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.TMDB;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Scheduling.Jobs.TMDB;
using Shoko.Server.Server;

#pragma warning disable CS0618
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

    private const string CharacterRole = "Character";

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

    public Role(TMDB_Movie_Cast cast)
    {
        var person = cast.GetTmdbPerson();
        if (person is null) { InitStub(cast.TmdbPersonID, CreatorRoleType.Actor, CharacterRole, cast.CharacterName); return; }
        var personImages = person.GetImages();
        Character = new() { Name = cast.CharacterName };
        Staff = new()
        {
            ID = person.Id,
            Name = person.EnglishName,
            AlternateName = person.Aliases.Count == 0 ? person.EnglishName : person.Aliases[0].Split("/").Last().Trim(),
            Description = person.EnglishBiography,
            Image = personImages.Count > 0 ? new Image(personImages[0]) : null,
        };
        RoleName = CreatorRoleType.Actor;
        RoleDetails = CharacterRole;
    }

    public Role(TMDB_Show_Cast cast)
    {
        var person = cast.GetTmdbPerson();
        if (person is null) { InitStub(cast.TmdbPersonID, CreatorRoleType.Actor, CharacterRole, cast.CharacterName); return; }
        var personImages = person.GetImages();
        Character = new() { Name = cast.CharacterName };
        Staff = new()
        {
            ID = person.Id,
            Name = person.EnglishName,
            AlternateName = person.Aliases.Count == 0 ? person.EnglishName : person.Aliases[0].Split("/").Last().Trim(),
            Description = person.EnglishBiography,
            Image = personImages.Count > 0 ? new Image(personImages[0]) : null,
        };
        RoleName = CreatorRoleType.Actor;
        RoleDetails = CharacterRole;
    }

    public Role(TMDB_Season_Cast cast)
    {
        var person = cast.GetTmdbPerson();
        if (person is null) { InitStub(cast.TmdbPersonID, CreatorRoleType.Actor, CharacterRole, cast.CharacterName); return; }
        var personImages = person.GetImages();
        Character = new() { Name = cast.CharacterName };
        Staff = new()
        {
            ID = person.Id,
            Name = person.EnglishName,
            AlternateName = person.Aliases.Count == 0 ? person.EnglishName : person.Aliases[0].Split("/").Last().Trim(),
            Description = person.EnglishBiography,
            Image = personImages.Count > 0 ? new Image(personImages[0]) : null,
        };
        RoleName = CreatorRoleType.Actor;
        RoleDetails = CharacterRole;
    }

    public Role(TMDB_Episode_Cast cast)
    {
        var person = cast.GetTmdbPerson();
        if (person is null) { InitStub(cast.TmdbPersonID, CreatorRoleType.Actor, CharacterRole, cast.CharacterName); return; }
        var personImages = person.GetImages();
        Character = new() { Name = cast.CharacterName };
        Staff = new()
        {
            ID = person.Id,
            Name = person.EnglishName,
            AlternateName = person.Aliases.Count == 0 ? person.EnglishName : person.Aliases[0].Split("/").Last().Trim(),
            Description = person.EnglishBiography,
            Image = personImages.Count > 0 ? new Image(personImages[0]) : null,
        };
        RoleName = CreatorRoleType.Actor;
        RoleDetails = CharacterRole;
    }

    public Role(TMDB_Movie_Crew crew)
    {
        var person = crew.GetTmdbPerson();
        if (person is null) { InitStub(crew.TmdbPersonID, crew.ToCreatorRole(), $"{crew.Department}, {crew.Job}"); return; }
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

    public Role(TMDB_Show_Crew crew)
    {
        var person = crew.GetTmdbPerson();
        if (person is null) { InitStub(crew.TmdbPersonID, crew.ToCreatorRole(), $"{crew.Department}, {crew.Job}"); return; }
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
        if (person is null) { InitStub(crew.TmdbPersonID, crew.ToCreatorRole(), $"{crew.Department}, {crew.Job}"); return; }
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
        if (person is null) { InitStub(crew.TmdbPersonID, crew.ToCreatorRole(), $"{crew.Department}, {crew.Job}"); return; }
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

    [MemberNotNull(nameof(Staff))]
    private void InitStub(int tmdbPersonId, CreatorRoleType roleType, string roleDetails, string? characterName = null)
    {
        var scheduler = ISystemService.StaticServices.GetRequiredService<IQueueScheduler>();
        _ = scheduler.Enqueue<UpdateTmdbPersonJob>(j => j.TmdbPersonID = tmdbPersonId);
        if (characterName is not null)
            Character = new() { Name = characterName };
        Staff = new() { ID = tmdbPersonId, Name = string.Empty, AlternateName = string.Empty, Description = string.Empty };
        RoleName = roleType;
        RoleDetails = roleDetails;
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
