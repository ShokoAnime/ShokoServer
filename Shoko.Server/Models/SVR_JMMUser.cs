using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using System.Linq;
using System.Security.Principal;
using ImageMagick;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Newtonsoft.Json;
using Shoko.Commons.Extensions;
using Shoko.Models.Client;
using Shoko.Models.Server;
using Shoko.Server.Extensions;
using Shoko.Server.Repositories;

namespace Shoko.Server.Models;

public class SVR_JMMUser : JMMUser, IIdentity
{
    public SVR_JMMUser()
    {
    }

    #region Image

    public class UserImageMetadata
    {
        public string ContentType;

        public int Width;

        public int Height;
    }

    public bool HasAvatarImage =>
        AvatarImageBlob != null && AvatarImageBlob.Length > 4 && !string.IsNullOrEmpty(RawAvatarImageMetadata) && RawAvatarImageMetadata.Length > 1;

    public byte[] AvatarImageBlob;

    internal string RawAvatarImageMetadata;

    private static readonly string[] AllowedMimeTypes = { "image/jpeg", "image/png", "image/bmp", "image/gif", "image/tiff", "image/webp" };

    public UserImageMetadata AvatarImageMetadata
    {
        get => !string.IsNullOrEmpty(RawAvatarImageMetadata) ? JsonConvert.DeserializeObject<UserImageMetadata>(RawAvatarImageMetadata) : null;
        set => RawAvatarImageMetadata = value == null ? null : JsonConvert.SerializeObject(value);
    }

    public bool SetAvatarImage(Stream stream, string contentType, string fieldName = "Image", ModelStateDictionary modelState = null)
    {
        // Check if the content-type is allowed.
        if (!AllowedMimeTypes.Contains(contentType))
        {
            modelState?.AddModelError(fieldName, "The provided content-type is not allowed.");
            return false;
        }

        try
        {
            // Check if content-type matches the actual image format.
            using var image = new MagickImage(stream);
            var expectedContentType = "image/" + image.Format.ToString().ToLower();
            if (expectedContentType != contentType)
            {
                modelState?.AddModelError(fieldName, "The provided content-type does not match the actual image format.");
                return false;
            }

            // Resize the image if it's larger than 512x512.
            if (image.Width > 512 || image.Height > 512)
            {
                var geometry = new MagickGeometry(512, 512) { IgnoreAspectRatio = false };
                image.Resize(geometry);
            }

            // Convert the image to WebP if it's not already WebP and set the
            // quality to 90%.
            image.Format = MagickFormat.WebP;
            image.Quality = 90;

            AvatarImageBlob = image.ToByteArray();
            AvatarImageMetadata = new UserImageMetadata()
            {
                Height = image.Height,
                Width = image.Width,
                ContentType = "image/webp",
            };
        }
        catch (Exception ex)
        {
            modelState?.AddModelError(fieldName, ex.Message);
            return false;
        }

        RepoFactory.JMMUser.Save(this, false);
        return true;
    }

    public void RemoveAvatarImage()
    {
        AvatarImageBlob = null;
        AvatarImageMetadata = null;
        RepoFactory.JMMUser.Save(this, false);
    }

    #endregion

    /// <summary>
    /// Returns whether a user is allowed to view this series
    /// </summary>
    /// <param name="ser"></param>
    /// <returns></returns>
    public bool AllowedSeries(SVR_AnimeSeries ser)
    {
        if (this.GetHideCategories().Count == 0) return true;
        var anime = ser?.GetAnime();
        if (anime == null) return false;
        return !this.GetHideCategories().FindInEnumerable(anime.GetTags().Select(a => a.TagName));
    }

    /// <summary>
    /// Returns whether a user is allowed to view this anime
    /// </summary>
    /// <param name="anime"></param>
    /// <returns></returns>
    public bool AllowedAnime(SVR_AniDB_Anime anime)
    {
        if (this.GetHideCategories().Count == 0) return true;
        return !this.GetHideCategories().FindInEnumerable(anime.GetTags().Select(a => a.TagName));
    }

    public bool AllowedGroup(SVR_AnimeGroup grp)
    {
        if (this.GetHideCategories().Count == 0) return true;
        if (grp.Contract == null) return false;
        return !this.GetHideCategories().FindInEnumerable(grp.Contract.Stat_AllTags);
    }

    public bool AllowedTag(AniDB_Tag tag)
    {
        return !this.GetHideCategories().Contains(tag.TagName);
    }

    public static bool CompareUser(JMMUser olduser, JMMUser newuser)
    {
        if (olduser == null || olduser.HideCategories == newuser.HideCategories)
            return true;
        return false;
    }

    public void UpdateGroupFilters()
    {
        IReadOnlyList<SVR_GroupFilter> gfs = RepoFactory.GroupFilter.GetAll();
        List<SVR_AnimeGroup> allGrps = RepoFactory.AnimeGroup.GetAllTopLevelGroups(); // No Need of subgroups
        foreach (SVR_GroupFilter gf in gfs)
        {
            bool change = false;
            foreach (SVR_AnimeGroup grp in allGrps)
            {
                CL_AnimeGroup_User cgrp = grp.GetUserContract(JMMUserID);
                change |= gf.UpdateGroupFilterFromGroup(cgrp, this);
            }
            if (change)
                RepoFactory.GroupFilter.Save(gf);
        }
    }

    // IUserIdentity implementation
    public string UserName
    {
        get { return Username; }
    }

    //[JsonIgnore]
    [NotMapped] public IEnumerable<string> Claims { get; set; }

    [NotMapped] string IIdentity.AuthenticationType => "API";

    [NotMapped] bool IIdentity.IsAuthenticated => true;

    [NotMapped] string IIdentity.Name => Username;


    public SVR_JMMUser(string username)
    {
        foreach (SVR_JMMUser us in RepoFactory.JMMUser.GetAll())
        {
            if (us.Username.ToLower() == username.ToLower())
            {
                JMMUserID = us.JMMUserID;
                Username = us.Username;
                Password = us.Password;
                IsAdmin = us.IsAdmin;
                IsAniDBUser = us.IsAniDBUser;
                IsTraktUser = us.IsTraktUser;
                HideCategories = us.HideCategories;
                CanEditServerSettings = us.CanEditServerSettings;
                PlexUsers = us.PlexUsers;
                Claims = us.Claims;
                break;
            }
        }
    }
}
