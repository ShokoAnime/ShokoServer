namespace Shoko.Server.API.v3.Models.Common;

/// <summary>
/// Works out the URL an image can be fetched from at its source, for the
/// image DTOs that offer one.
/// </summary>
public static class RemoteImageUrl
{
    /// <summary>
    /// The URL for <paramref name="resourceID"/> at its source, or <c>null</c>
    /// when the caller did not ask for one, the image is available locally and
    /// only the ones that are not were asked for, or the source has no
    /// template registered to build a URL from.
    /// </summary>
    /// <param name="template">The source's template URL, from
    /// <c>IImageManager.GetTemplateUrlForSource</c>.</param>
    /// <param name="resourceID">The image's resource identifier.</param>
    /// <param name="isAvailable">Whether the image is available locally.</param>
    /// <param name="include">Which images to build a URL for.</param>
    public static string? Resolve(string? template, string resourceID, bool isAvailable, RemoteUrlInclusion include)
    {
        if (string.IsNullOrEmpty(template))
            return null;

        return include switch
        {
            RemoteUrlInclusion.True => string.Format(template, resourceID),
            RemoteUrlInclusion.WhenUnavailable when !isAvailable => string.Format(template, resourceID),
            _ => null,
        };
    }
}
