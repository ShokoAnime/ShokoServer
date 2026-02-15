using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Exceptions;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Services;
using Shoko.Abstractions.User;
using Shoko.Server.Extensions;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.API.v3.Models.Shoko;

public class User
{
    /// <summary>
    /// The UserID, this is used in a lot of v1 and v2 endpoints, and it's needed for editing or removing a user
    /// </summary>
    public int ID { get; set; }

    /// <summary>
    /// Pretty Self-explanatory. It's the Username of the user
    /// </summary>
    public string Username { get; set; }

    /// <summary>
    /// Is the user an admin. Admins can perform all operations, including modification of users
    /// </summary>
    public bool IsAdmin { get; set; }

    /// <summary>
    /// This is a list of services that the user is set to use. AniDB, Trakt, and Plex, for example
    /// </summary>
    [JsonProperty(ItemConverterType = typeof(StringEnumConverter))]
    public List<CommunitySite> CommunitySites { get; set; }

    /// <summary>
    /// Restricted tags. Any group/series containing any of these tags will be
    /// rendered inaccessible to the user.
    /// </summary>
    public List<int> RestrictedTags { get; set; }

    /// <summary>
    /// The user's avatar as a base64 encoded data url if available. Otherwise
    /// an empty string.
    /// </summary>
    public string Avatar { get; set; }

    /// <summary>
    /// The user's Plex usernames.
    /// </summary>
    public string PlexUsernames { get; set; }

    public User(IUser user) : this((JMMUser)user) { }

    public User(JMMUser user)
    {
        ID = user.JMMUserID;
        Username = user.Username;
        IsAdmin = user.IsAdmin == 1;
        CommunitySites = [];
        if (user.IsAniDBUser == 1)
            CommunitySites.Add(CommunitySite.AniDB);
        if (user.IsTraktUser == 1)
            CommunitySites.Add(CommunitySite.Trakt);
        if (!string.IsNullOrEmpty(user.PlexToken))
            CommunitySites.Add(CommunitySite.Plex);

        RestrictedTags = user.GetHideTags()
            .Select(tag => tag.TagID)
            .ToList();

        Avatar = user.GetAvatarImageAsDataURL();

        PlexUsernames = user.PlexUsers ?? string.Empty;
    }

    public class Input
    {
        public class CreateUserBody : CreateOrUpdateUserBody
        {
            /// <summary>
            /// The new password.
            /// </summary>
            /// <remarks>
            /// Shoko is NOT secure, so don't assume this password is safe or even necessary to access the account.
            /// </remarks>
            [MaxLength(1024,
                ErrorMessage =
                    "Password cannot be longer than 1024 characters. Why the fuck do you need more than 1024 characters in your password?")]
            [Required(ErrorMessage = "Password is required", AllowEmptyStrings = true)]
            public string? Password { get; set; } = string.Empty;

            public CreateUserBody() : base() { }

            public User? Save(ModelStateDictionary modelState, bool isAdmin = false)
            {
                if (RestrictedTags is not null && !isAdmin)
                    modelState.AddModelError(nameof(RestrictedTags), "Only admins are allowed to change the restricted tags for users.");

                if (CommunitySites is not null && !isAdmin)
                    modelState.AddModelError(nameof(CommunitySites), "Only admins are allowed to change the community sites for users.");

                if (IsAdmin.HasValue && !isAdmin)
                    modelState.AddModelError(nameof(IsAdmin), "Only admins are allowed to change the admin status of users.");

                if (!modelState.IsValid)
                    return null;

                try
                {
                    var service = Utils.ServiceContainer.GetRequiredService<IUserService>();
                    var initialData = new UserUpdateData();
                    if (Username is not null)
                        initialData.Username = Username;
                    if (Password is not null)
                        initialData.Password = Password;
                    if (IsAdmin.HasValue)
                        initialData.IsAdmin = IsAdmin.Value;
                    if (CommunitySites is not null)
                        initialData.IsAnidbUser = CommunitySites.Contains(CommunitySite.AniDB);
                    if (RestrictedTags is not null)
                        initialData.RestrictedTags = RestrictedTags
                            .Select(RepoFactory.AniDB_Tag.GetByTagID)
                            .WhereNotNull()
                            .Cast<IAnidbTag>()
                            .ToList();
                    if (Avatar is not null)
                        initialData.AvatarImage = Avatar is "" ? null : Encoding.UTF8.GetBytes(Avatar);

                    // This will throw a validation error of something is invalid.
                    var user = (JMMUser)service.CreateUser(initialData)
                        .ConfigureAwait(false)
                        .GetAwaiter()
                        .GetResult();

                    // Extra handling for things not exposed to the plugin API
                    // and probably never will.
                    var saved = false;
                    if (CommunitySites is not null)
                    {
                        var oldTraktUser = user.IsTraktUser == 1;
                        var newTraktUser = CommunitySites.Contains(CommunitySite.Trakt);
                        if (oldTraktUser != newTraktUser)
                        {
                            saved = true;
                            user.IsTraktUser = CommunitySites.Contains(CommunitySite.Trakt) ? 1 : 0;
                        }
                    }

                    if (PlexUsernames is not null)
                    {
                        var oldPlexUsernames = user.PlexUsers;
                        var newPlexUsernames = string.IsNullOrWhiteSpace(PlexUsernames) ? null : PlexUsernames;
                        if (oldPlexUsernames != newPlexUsernames)
                        {
                            saved = true;
                            user.PlexUsers = string.IsNullOrWhiteSpace(PlexUsernames) ? null : PlexUsernames;
                        }
                    }

                    if (!saved)
                    {
                        RepoFactory.JMMUser.Save(user);
                    }

                    return new User(user);
                }
                catch (GenericValidationException ex)
                {
                    foreach (var (key, errors) in ex.ValidationErrors)
                        foreach (var value in errors)
                            modelState.AddModelError(key, value);

                    return null;
                }
            }
        }

        public class CreateOrUpdateUserBody
        {
            /// <summary>
            /// The user's new name. Must not be empty or only white-spaces.
            /// </summary>
            public string? Username { get; set; }

            /// <summary>
            /// Change the user admin status. The viewer must have admin access
            /// yo change this.
            /// </summary>
            public bool? IsAdmin { get; set; }

            /// <summary>
            /// The updated list of services that the user can use. The viewer
            /// must have admin access to change these.
            /// </summary>
            public List<CommunitySite>? CommunitySites { get; set; }

            /// <summary>
            /// The updated restricted tags for the user. The viewer must have
            /// admin access to change these.
            /// </summary>
            public List<int>? RestrictedTags { get; set; }

            /// <summary>
            /// The new user's avatar image, base64 encoded. Set to an empty
            /// string to remove the current avatar image.
            /// </summary>
            public string? Avatar { get; set; } = null;

            /// <summary>
            /// The new user's Plex usernames.
            /// </summary>
            public string? PlexUsernames { get; set; }

            public CreateOrUpdateUserBody() { }

            public virtual User? MergeWithExisting(IUser user, ModelStateDictionary modelState, bool isAdmin = false)
                => MergeWithExisting((JMMUser)user, modelState, isAdmin);

            public virtual User? MergeWithExisting(JMMUser user, ModelStateDictionary modelState, bool isAdmin = false)
            {
                if (RestrictedTags is not null && !isAdmin)
                    modelState.AddModelError(nameof(RestrictedTags), "Only admins are allowed to change the restricted tags for users.");

                if (CommunitySites is not null && !isAdmin)
                    modelState.AddModelError(nameof(CommunitySites), "Only admins are allowed to change the community sites for users.");

                if (IsAdmin.HasValue && user.IsAdminUser() != IsAdmin.Value)
                {
                    if (isAdmin)
                    {
                        var allAdmins = RepoFactory.JMMUser.GetAll().Where(a => a.IsAdminUser()).ToList();
                        allAdmins.Remove(user);
                        if (allAdmins.Count < 1)
                            modelState.AddModelError(nameof(IsAdmin), "There must be at least one admin user.");
                    }
                    else
                    {
                        modelState.AddModelError(nameof(IsAdmin), "Only admins are allowed to change the admin status of users.");
                    }
                }

                if (!modelState.IsValid)
                    return null;

                try
                {
                    var service = Utils.ServiceContainer.GetRequiredService<IUserService>();
                    var updateData = new UserUpdateData();
                    if (Username is not null)
                        updateData.Username = Username;
                    if (IsAdmin.HasValue)
                        updateData.IsAdmin = IsAdmin.Value;
                    if (CommunitySites is not null)
                        updateData.IsAnidbUser = CommunitySites.Contains(CommunitySite.AniDB);
                    if (RestrictedTags is not null)
                        updateData.RestrictedTags = RestrictedTags
                            .Select(RepoFactory.AniDB_Tag.GetByTagID)
                            .WhereNotNull()
                            .Cast<IAnidbTag>()
                            .ToList();
                    if (Avatar is not null)
                        updateData.AvatarImage = Avatar is "" ? null : Encoding.UTF8.GetBytes(Avatar);

                    // This will throw a validation error of something is invalid.
                    user = (JMMUser)service.UpdateUser(user, updateData)
                        .ConfigureAwait(false)
                        .GetAwaiter()
                        .GetResult();

                    // Extra handling for things not exposed to the plugin API
                    // and probably never will.
                    var saved = false;
                    if (CommunitySites is not null)
                    {
                        var oldTraktUser = user.IsTraktUser == 1;
                        var newTraktUser = CommunitySites.Contains(CommunitySite.Trakt);
                        if (oldTraktUser != newTraktUser)
                        {
                            saved = true;
                            user.IsTraktUser = CommunitySites.Contains(CommunitySite.Trakt) ? 1 : 0;
                        }
                    }

                    if (PlexUsernames is not null)
                    {
                        var oldPlexUsernames = user.PlexUsers;
                        var newPlexUsernames = string.IsNullOrWhiteSpace(PlexUsernames) ? null : PlexUsernames;
                        if (oldPlexUsernames != newPlexUsernames)
                        {
                            saved = true;
                            user.PlexUsers = string.IsNullOrWhiteSpace(PlexUsernames) ? null : PlexUsernames;
                        }
                    }

                    if (!saved)
                    {
                        RepoFactory.JMMUser.Save(user);
                    }

                    return new User(user);
                }
                catch (GenericValidationException ex)
                {
                    foreach (var (key, errors) in ex.ValidationErrors)
                        foreach (var value in errors)
                            modelState.AddModelError(key, value);

                    return null;
                }
            }
        }

        public class ChangePasswordBody
        {
            public ChangePasswordBody()
            {
                Password = "";
                RevokeAPIKeys = true;
            }

            /// <summary>
            /// Password
            /// </summary>
            /// <value></value>
            [MaxLength(1024,
                ErrorMessage =
                    "Password cannot be longer than 1024 characters. Why the fuck do you need more than 1024 characters in your password?")]
            [Required(ErrorMessage = "Password is required", AllowEmptyStrings = true)]
            public string Password { get; set; }

            /// <summary>
            /// Revoke all previous active API keys for the user.
            /// </summary>
            /// <value></value>
            [DefaultValue(true)]
            public bool RevokeAPIKeys { get; set; }
        }
    }
}
