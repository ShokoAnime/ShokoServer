using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using ImageMagick;
using Newtonsoft.Json;
using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.User;
using Shoko.Server.Extensions;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.Shoko;

public class JMMUser : IIdentity, IUser
{
    #region Database Columns

    public int JMMUserID { get; set; }

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public int IsAdmin { get; set; }

    public int IsAniDBUser { get; set; }

    public int IsTraktUser { get; set; }

    public string? HideCategories { get; set; }

    public int? CanEditServerSettings { get; set; }

    public string? PlexUsers { get; set; }

    public string? PlexToken { get; set; }

    #endregion

    #region Image

    public class UserImageMetadata
    {
        public string ContentType { get; set; } = string.Empty;

        public int Width { get; set; }

        public int Height { get; set; }
    }

    private class AbstractUserImage(int userId, UserImageMetadata metadata, byte[] avatarImage) : IImage
    {
        public ImageEntityType ImageType => ImageEntityType.Art;

        public string ContentType => metadata.ContentType;

        public bool IsEnabled => true;

        public bool IsPreferred => true;

        public bool IsLocked => false;

        public bool IsLocalAvailable => false;

        public bool IsRemoteAvailable => false;

        public double AspectRatio => Width / Height;

        public int Width => metadata.Width;

        public int Height => metadata.Height;

        public string? LanguageCode => null;

        public TitleLanguage Language => TitleLanguage.Unknown;

        public string? RemoteURL => null;

        public string? LocalPath => null;

        public int ID => userId;

        public DataSource Source => DataSource.User;

        public Task<bool> DownloadImage(bool force = false)
            => Task.FromResult(false);

        public bool Equals(IImage? other)
            => other is not null && other.Source == Source && other.ImageType == ImageType && other.ID == ID;

        public Stream? GetStream()
            => new MemoryStream(avatarImage);
    }

    [MemberNotNullWhen(true, nameof(AvatarImageBlob), nameof(AvatarImageMetadata))]
    public bool HasAvatarImage =>
        AvatarImageBlob != null && AvatarImageBlob.Length > 4 && !string.IsNullOrEmpty(RawAvatarImageMetadata) && RawAvatarImageMetadata.Length > 1;

    public byte[]? AvatarImageBlob;

    internal string? RawAvatarImageMetadata;

    private static readonly string[] _allowedMimeTypes = ["image/jpeg", "image/png", "image/bmp", "image/gif", "image/tiff", "image/webp"];

    public UserImageMetadata? AvatarImageMetadata
    {
        get => !string.IsNullOrEmpty(RawAvatarImageMetadata) ? JsonConvert.DeserializeObject<UserImageMetadata>(RawAvatarImageMetadata) : null;
        set => RawAvatarImageMetadata = value == null ? null : JsonConvert.SerializeObject(value);
    }

    public string GetAvatarImageAsDataURL()
    {
        if (!HasAvatarImage)
            return string.Empty;

        try
        {
            var base64 = Convert.ToBase64String(AvatarImageBlob);
            return $"data:{AvatarImageMetadata.ContentType};base64,{base64}";
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private const long MaxFileSize = 8 * 1024 * 1024; // 8MiB in bytes

    public bool SetAvatarImage(Stream? stream, string fieldName, Action<string, string> addModelError)
        => SetAvatarImage(stream?.ToByteArray(), fieldName, addModelError);

    public bool SetAvatarImage(byte[]? byteArray, string fieldName, Action<string, string> addModelError)
    {
        // Unset/remove avatar image.
        if (byteArray is null)
        {
            if (!HasAvatarImage)
                return false;

            AvatarImageBlob = null;
            AvatarImageMetadata = null;
            return true;
        }

        if (!TryGetFromDataURL(byteArray, fieldName, addModelError, out byteArray, out var contentType))
            return false;

        if (byteArray.Length > MaxFileSize)
        {
            addModelError(fieldName, "Avatar image file size cannot exceed 8MiB (after deserializing).");
            return false;
        }

        contentType = contentType?.Replace("jpg", "jpeg");
        // Check if the content-type is allowed.
        if (contentType is not null && !_allowedMimeTypes.Contains(contentType))
        {
            addModelError(fieldName, "The provided content-type is not allowed.");
            return false;
        }

        try
        {
            // Check if content-type matches the actual image format.
            var image = new MagickImageInfo(byteArray);
            var expectedContentType = "image/" + image.Format.ToString().ToLower().Replace("jpg", "jpeg");
            if (contentType is null)
            {
                if (!_allowedMimeTypes.Contains(expectedContentType))
                {
                    addModelError(fieldName, "The provided image format is not allowed.");
                    return false;
                }
            }
            else
            {
                if (expectedContentType != contentType)
                {
                    addModelError(fieldName, "The provided content-type does not match the actual image format.");
                    return false;
                }
            }

            // Reject the image if it's larger than 512x512.
            if (image.Width > 512 || image.Height > 512)
            {
                addModelError(fieldName, "The provided image cannot be larger than 512x512.");
                return false;
            }

            // Do a conditional compare between the current blob and new blob.
            if (HasAvatarImage && AvatarImageBlob.SequenceEqual(byteArray))
                return false;

            AvatarImageBlob = byteArray;
            AvatarImageMetadata = new UserImageMetadata()
            {
                Height = (int)image.Height,
                Width = (int)image.Width,
                ContentType = expectedContentType,
            };
        }
        catch (Exception ex)
        {
            addModelError(fieldName, ex.Message);
            return false;
        }

        return true;
    }

    private static readonly string[] _dataUrlSeparators = [":", ";", ","];

    public static bool TryGetFromDataURL(
        byte[] maybeDataUrl,
        string fieldName,
        Action<string, string> addModelError,
        [NotNullWhen(true)] out byte[]? byteArray,
        out string? contentType
    )
    {
        if (maybeDataUrl.Length < 16 || maybeDataUrl[0] != 'd' || maybeDataUrl[1] != 'a' || maybeDataUrl[2] != 't' || maybeDataUrl[3] != 'a' || maybeDataUrl[4] != ':')
        {
            byteArray = maybeDataUrl;
            contentType = null;
            return true;
        }

        var parts = Encoding.UTF8.GetString(maybeDataUrl).Split(_dataUrlSeparators, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4 || parts[0] != "data")
        {
            addModelError(fieldName, $"Invalid data URL format for field '{fieldName}'.");
            byteArray = null;
            contentType = null;
            return false;
        }

        try
        {
            byteArray = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            addModelError(fieldName, $"Base64 data is not in a correct format for field '{fieldName}'.");
            byteArray = null;
            contentType = null;
            return false;
        }
        catch (Exception)
        {
            addModelError(fieldName, $"Unexpected error when converting data URL to byte array for field '{fieldName}'.");
            byteArray = null;
            contentType = null;
            return false;
        }

        contentType = parts[1];
        return true;
    }

    #endregion

    /// <summary>
    /// Returns whether a user is allowed to view this series
    /// </summary>
    /// <param name="ser"></param>
    /// <returns></returns>
    public bool AllowedSeries(AnimeSeries ser)
    {
        if (GetHideCategories().Count == 0) return true;
        var anime = ser?.AniDB_Anime;
        if (anime == null) return false;
        return !GetHideCategories().FindInEnumerable(anime.Tags.Select(a => a.TagName));
    }

    /// <summary>
    /// Returns whether a user is allowed to view this anime
    /// </summary>
    /// <param name="anime"></param>
    /// <returns></returns>
    public bool AllowedAnime(AniDB_Anime anime)
    {
        if (GetHideCategories().Count == 0) return true;
        return !GetHideCategories().FindInEnumerable(anime.Tags.Select(a => a.TagName));
    }

    public bool AllowedGroup(AnimeGroup grp)
    {
        if (GetHideCategories().Count == 0) return true;
        return !GetHideCategories().FindInEnumerable(grp.Tags.Select(a => a.TagName));
    }

    public bool AllowedTag(AniDB_Tag tag)
    {
        return !GetHideCategories().Contains(tag.TagName);
    }

    public HashSet<string> GetHideCategories()
    {
        if (string.IsNullOrEmpty(HideCategories)) return new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        return new HashSet<string>(HideCategories.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            StringComparer.InvariantCultureIgnoreCase);
    }

    public List<AniDB_Tag> GetHideTags()
        => GetHideCategories()
        .SelectMany(RepoFactory.AniDB_Tag.GetByName)
        .WhereNotNull()
        .OrderBy(tag => tag.TagID)
        .ToList();

    [NotMapped]
    string IIdentity.AuthenticationType => "API";

    [NotMapped]
    bool IIdentity.IsAuthenticated => true;

    [NotMapped]
    string IIdentity.Name => Username;

    [NotMapped]
    IImage? IWithPortraitImage.PortraitImage => HasAvatarImage
        ? new AbstractUserImage(JMMUserID, AvatarImageMetadata, AvatarImageBlob)
        : null;

    [NotMapped]
    int IUser.ID => JMMUserID;

    [NotMapped]
    string IUser.Username => Username;

    [NotMapped]
    bool IUser.IsAdmin => IsAdmin == 1;

    [NotMapped]
    bool IUser.IsAnidbUser => IsAniDBUser == 1;

    [NotMapped]
    IReadOnlyList<IAnidbTag> IUser.RestrictedTags => GetHideTags();
}
