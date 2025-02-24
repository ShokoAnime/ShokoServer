using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

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

    public User(SVR_JMMUser user)
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

        RestrictedTags = user.GetHideCategories()
            .Select(name => RepoFactory.AniDB_Tag.GetByName(name).FirstOrDefault()!)
            .Where(tag => tag != null)
            .Select(tag => tag.TagID)
            .ToList();

        Avatar = user.HasAvatarImage ? ModelHelper.ToDataURL(user.AvatarImageBlob, user.AvatarImageMetadata.ContentType) ?? string.Empty : string.Empty;

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
                var user = new SVR_JMMUser()
                {
                    Password = string.IsNullOrEmpty(Password) ? string.Empty : Digest.Hash(Password),
                };
                return MergeWithExisting(user, modelState, isAdmin);
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

            private const long MaxFileSize = 8 * 1024 * 1024; // 8MiB in bytes

            public virtual User? MergeWithExisting(SVR_JMMUser user, ModelStateDictionary modelState, bool isAdmin = false)
            {
                if (Username != null && string.IsNullOrWhiteSpace(Username))
                    modelState.AddModelError(nameof(Username), "Username cannot be empty or only white-spaces.");

                if ((user.JMMUserID == 0 || string.IsNullOrEmpty(user.Username)) && string.IsNullOrWhiteSpace(Username))
                    modelState.AddModelError(nameof(Username), "A new user must have a username set.");

                {
                    var existingUser = RepoFactory.JMMUser.GetByUsername(Username);
                    if (existingUser != null && existingUser.JMMUserID != user.JMMUserID)
                        modelState.AddModelError(nameof(Username), "The username is unavailable.");
                }

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

                if (RestrictedTags != null && !isAdmin)
                    modelState.AddModelError(nameof(RestrictedTags), "Only admins are allowed to change the restricted tags for users.");

                if (CommunitySites != null && !isAdmin)
                    modelState.AddModelError(nameof(CommunitySites), "Only admins are allowed to change the community sites for users.");

                var (byteArray, contentType) = string.IsNullOrWhiteSpace(Avatar) ? (null, null) : ModelHelper.FromDataURL(Avatar, nameof(Avatar), modelState);
                if (!string.IsNullOrEmpty(contentType) && byteArray != null && byteArray.Length > MaxFileSize)
                    modelState.AddModelError(nameof(Avatar), "Avatar image file size cannot exceed 8MiB (after deserializing).");

                // Return early if the model state was invalidated.
                if (!modelState.IsValid)
                    return null;

                // Try to update the avatar for the user.
                if (Avatar != null)
                {
                    if (contentType == null || byteArray == null)
                    {
                        user.RemoveAvatarImage(skipSave: true);
                    }
                    else
                    {
                        user.SetAvatarImage(byteArray, contentType, "Avatar", modelState, skipSave: true);
                        // Return now if the model state was invalidated.
                        if (!modelState.IsValid)
                            return null;
                    }
                }

                // Update the username for the user.
                if (!string.IsNullOrEmpty(Username))
                    user.Username = Username.Trim();

                // Update the admin status for the user.
                if (IsAdmin.HasValue)
                {
                    user.IsAdmin = IsAdmin.Value ? 1 : 0;
                    user.CanEditServerSettings = IsAdmin.Value ? 1 : 0;
                }

                // Update restricted tags for the user.
                if (RestrictedTags != null)
                {
                    var tags = RestrictedTags
                        .Select(RepoFactory.AniDB_Tag.GetByTagID)
                        .WhereNotNull()
                        .Select(tag => tag.TagName);
                    user.HideCategories = string.Join(',', tags);
                }

                // Update the community sites for the user.
                if (CommunitySites != null)
                {
                    user.IsTraktUser = CommunitySites.Contains(CommunitySite.Trakt) ? 1 : 0;
                    user.IsAniDBUser = CommunitySites.Contains(CommunitySite.AniDB) ? 1 : 0;
                }

                if (PlexUsernames != null)
                {
                    user.PlexUsers = string.IsNullOrWhiteSpace(PlexUsernames) ? null : PlexUsernames;
                }

                // Save the model now.
                RepoFactory.JMMUser.Save(user);

                return new User(user);
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
