namespace Shoko.Server.API.v3.Models.ImageManagement.Input;

/// <summary>
///   Body for setting a template URL for an image source.
/// </summary>
public class SetTemplateUrlBody
{
    /// <summary>
    ///   The template URL to set. Use <c>null</c> or empty to clear.
    /// </summary>
    public string? TemplateUrl { get; set; }
}
