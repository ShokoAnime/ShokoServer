using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Security.Principal;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.User;
using Shoko.Server.Extensions;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Repositories;

#pragma warning disable CS0618
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

    public string? HideCategories { get; set; }

    public int? CanEditServerSettings { get; set; }

    public string? PlexUsers { get; set; }

    public string? PlexToken { get; set; }

    #endregion

    public string GetAvatarImageAsDataURL()
    {
        if ((this as IWithPrimaryImage).PrimaryImage is not { IsAvailable: true } primaryImage)
            return string.Empty;

        try
        {
            var byteArray = primaryImage.GetStream()?.ToByteArray();
            if (byteArray is null)
                return string.Empty;

            var base64 = Convert.ToBase64String(byteArray);
            return $"data:{primaryImage.ContentType};base64,{base64}";
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

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

    #region IIdentity Implementation

    [NotMapped]
    string IIdentity.AuthenticationType => "API";

    [NotMapped]
    bool IIdentity.IsAuthenticated => true;

    [NotMapped]
    string IIdentity.Name => Username;

    #endregion

    #region IMetadata Implementation

    [NotMapped]
    DataEntityType IMetadata.EntityType => DataEntityType.User;

    [NotMapped]
    DataSource IMetadata.Source => DataSource.Shoko;

    [NotMapped]
    int IMetadata<int>.ID => JMMUserID;

    #endregion

    #region IWithImages Implementation

    public IImage? GetPreferredImageForType(ImageEntityType imageType)
        => GetImages(imageType: imageType).FirstOrDefault(image => image.IsPreferred);

    public IImageCrossReference? GetPreferredImageCrossReferenceForType(ImageEntityType imageType)
        => GetImageCrossReferences(imageType: imageType).FirstOrDefault(xref => xref.IsPreferred);

    public IReadOnlyList<IImage> GetImages(DataSource? imageSource = null, ImageEntityType? imageType = null, DataSource? xrefSource = null, bool? isEnabled = null, bool? isDesired = null, bool primaryImage = false)
        => ISystemService.StaticServices.GetRequiredService<IImageManager>()
            .GetImagesForEntity(this, imageSource, imageType, xrefSource, isEnabled, isDesired, primaryImage);

    public IReadOnlyList<IImageCrossReference> GetImageCrossReferences(DataSource? imageSource = null, ImageEntityType? imageType = null, DataSource? xrefSource = null, bool? isEnabled = null, bool? isDesired = null)
        => ISystemService.StaticServices.GetRequiredService<IImageManager>()
            .GetImageCrossReferencesForEntity(this, imageSource, imageType, xrefSource, isEnabled, isDesired);

    #endregion

    #region IUser Implementation

    [NotMapped]
    string IUser.Username => Username;

    [NotMapped]
    bool IUser.IsAdmin => IsAdmin == 1;

    [NotMapped]
    bool IUser.IsAnidbUser => IsAniDBUser == 1;

    [NotMapped]
    IReadOnlyList<IAnidbTag> IUser.RestrictedTags => GetHideTags();

    #endregion
}
