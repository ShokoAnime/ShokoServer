using System.Collections.Generic;
using Shoko.Abstractions.Plugin.Models;

namespace Shoko.Abstractions.Plugin;

/// <summary>
///   The licences of the third-party packages and assets the server ships: the
///   core's own and every loaded plugin's, read from the
///   <c>third-party-notices.json.br</c> file each build puts beside its assembly.
///   A plugin needs no code to be listed; see the notices file format in the
///   <c>Shoko.BuildTools</c> README.
/// </summary>
public interface IThirdPartyNoticeService
{
    /// <summary>
    ///   The notices, one section per plugin that ships any: the core first,
    ///   then the active plugins in load order. The texts are left out, since
    ///   they are most of the size; read them with <see cref="GetText"/>.
    /// </summary>
    /// <returns>The sections.</returns>
    IReadOnlyList<ThirdPartyNoticeSection> GetSections();

    /// <summary>
    ///   A licence or notice text by its key, from any plugin's notices.
    /// </summary>
    /// <param name="key">The text's key, as <see cref="ThirdPartyNotice.TextKeys"/> lists it.</param>
    /// <returns>The text, or <c>null</c> when no notices have it.</returns>
    string? GetText(string key);
}
