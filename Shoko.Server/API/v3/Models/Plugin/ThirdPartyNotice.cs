using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Plugin.Enums;

using AbstractThirdPartyNotice = Shoko.Abstractions.Plugin.Models.ThirdPartyNotice;

namespace Shoko.Server.API.v3.Models.Plugin;

/// <summary>
/// The licence of one third-party package or asset a plugin ships, or of the plugin itself.
/// </summary>
public class ThirdPartyNotice(AbstractThirdPartyNotice notice)
{
    /// <summary>
    /// The package ID, or the asset's name.
    /// </summary>
    [Required]
    public string ID { get; init; } = notice.ID;

    /// <summary>
    /// The version, if known.
    /// </summary>
    public string? Version { get; init; } = notice.Version;

    /// <summary>
    /// Where it comes from.
    /// </summary>
    [Required]
    public ThirdPartyNoticeSource Source { get; init; } = notice.Source;

    /// <summary>
    /// The licence as an SPDX expression, as the package writes it, or <see langword="null"/> when it only gives a
    /// licence text.
    /// </summary>
    public string? License { get; init; } = notice.License;

    /// <summary>
    /// The copyright line, if any.
    /// </summary>
    public string? Copyright { get; init; } = notice.Copyright;

    /// <summary>
    /// The project or licence URL, if any.
    /// </summary>
    public string? Url { get; init; } = notice.Url;

    /// <summary>
    /// The keys of its licence and notice texts, licence texts first, read through
    /// <c>GET /api/v3/ThirdPartyNotice/Text</c>.
    /// </summary>
    [Required]
    public IReadOnlyList<string> TextKeys { get; init; } = notice.TextKeys;
}
