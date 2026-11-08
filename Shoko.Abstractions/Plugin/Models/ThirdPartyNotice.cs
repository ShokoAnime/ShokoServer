using System.Collections.Generic;
using Shoko.Abstractions.Plugin.Enums;

namespace Shoko.Abstractions.Plugin.Models;

/// <summary>
///   The licence of one third-party package or asset a plugin ships, or of the
///   plugin itself.
/// </summary>
public sealed class ThirdPartyNotice
{
    /// <summary>
    ///   The package ID, or the asset's name.
    /// </summary>
    public required string ID { get; init; }

    /// <summary>
    ///   The version, if known.
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    ///   Where it comes from.
    /// </summary>
    public required ThirdPartyNoticeSource Source { get; init; }

    /// <summary>
    ///   The licence as an SPDX expression, as the package writes it, or
    ///   <c>null</c> when it only gives a licence text.
    /// </summary>
    public string? License { get; init; }

    /// <summary>
    ///   The copyright line, if any.
    /// </summary>
    public string? Copyright { get; init; }

    /// <summary>
    ///   The project or licence URL, if any.
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    ///   The keys of its licence and notice texts, licence texts first, read
    ///   through <see cref="IThirdPartyNoticeService.GetText"/>.
    /// </summary>
    public required IReadOnlyList<string> TextKeys { get; init; }
}
