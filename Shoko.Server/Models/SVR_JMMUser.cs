using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Security.Principal;
using ImageMagick;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Newtonsoft.Json;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.DataModels.Shoko;
using Shoko.Server.Extensions;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Repositories;

namespace Shoko.Server.Models;

public class SVR_JMMUser : JMMUser, IIdentity, IShokoUser
{
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

    public bool SetAvatarImage(byte[] byteArray, string contentType, string fieldName = "Image", ModelStateDictionary modelState = null, bool skipSave = false)
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
            var image = new MagickImageInfo(byteArray);
            var expectedContentType = "image/" + image.Format.ToString().ToLower();
            if (expectedContentType != contentType)
            {
                modelState?.AddModelError(fieldName, "The provided content-type does not match the actual image format.");
                return false;
            }

            // Reject the image if it's larger than 512x512.
            if (image.Width > 512 || image.Height > 512)
            {
                modelState?.AddModelError(fieldName, "The provided image cannot be larger than 512x512.");
                return false;
            }

            AvatarImageBlob = byteArray;
            AvatarImageMetadata = new UserImageMetadata()
            {
                Height = image.Height,
                Width = image.Width,
                ContentType = contentType,
            };
        }
        catch (Exception ex)
        {
            modelState?.AddModelError(fieldName, ex.Message);
            return false;
        }

        if (!skipSave)
            RepoFactory.JMMUser.Save(this);

        return true;
    }

    public void RemoveAvatarImage(bool skipSave = false)
    {
        AvatarImageBlob = null;
        AvatarImageMetadata = null;

        if (!skipSave)
            RepoFactory.JMMUser.Save(this);
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
        var anime = ser?.AniDB_Anime;
        if (anime == null) return false;
        return !this.GetHideCategories().FindInEnumerable(anime.Tags.Select(a => a.TagName));
    }

    /// <summary>
    /// Returns whether a user is allowed to view this anime
    /// </summary>
    /// <param name="anime"></param>
    /// <returns></returns>
    public bool AllowedAnime(SVR_AniDB_Anime anime)
    {
        if (this.GetHideCategories().Count == 0) return true;
        return !this.GetHideCategories().FindInEnumerable(anime.Tags.Select(a => a.TagName));
    }

    public bool AllowedGroup(SVR_AnimeGroup grp)
    {
        if (this.GetHideCategories().Count == 0) return true;
        return !this.GetHideCategories().FindInEnumerable(grp.Tags.Select(a => a.TagName));
    }

    public bool AllowedTag(AniDB_Tag tag)
    {
        return !this.GetHideCategories().Contains(tag.TagName);
    }

    [NotMapped] string IIdentity.AuthenticationType => "API";

    [NotMapped] bool IIdentity.IsAuthenticated => true;

    [NotMapped] string IIdentity.Name => Username;

    [NotMapped]
    int IShokoUser.ID => JMMUserID;

    [NotMapped]
    string IShokoUser.Username => Username;
}
