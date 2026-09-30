using Shoko.Abstractions.Metadata;

namespace Shoko.Server.Models.Interfaces;

/// <summary>
///   An entry whose own row keeps its source's default title or overview,
///   which the text manager reads as a candidate beside the stored texts.
/// </summary>
/// <remarks>
///   The default stays on the row and its provider keeps writing it there; it
///   is never copied into the stored texts. A store that saves the row with
///   its texts tells the manager through the text store; a provider saving it
///   any other way calls <c>IMetadataTextManager.Invalidate</c>.
/// </remarks>
internal interface IInlineTextSource
{
    /// <summary>
    ///   The default title on the entry's row.
    /// </summary>
    /// <value>
    ///   The title, marked with <see cref="IText.IsInlineDefault"/> and in the
    ///   language the entry has always given it, or <c>null</c> when the row
    ///   has none.
    /// </value>
    ITitle? InlineTitle { get; }

    /// <summary>
    ///   The default overview on the entry's row.
    /// </summary>
    /// <value>
    ///   The overview, marked with <see cref="IText.IsInlineDefault"/> and in
    ///   the language the entry has always given it, or <c>null</c> when the
    ///   row has none.
    /// </value>
    IText? InlineOverview { get; }

    /// <summary>
    ///   Where the default title sits in the entry's list of titles.
    /// </summary>
    /// <value>
    ///   <see cref="InlineTextPlacement.First"/> unless the entry's source
    ///   lists it among its other titles.
    /// </value>
    InlineTextPlacement InlineTitlePlacement { get => InlineTextPlacement.First; }

    /// <summary>
    ///   Where the default overview sits in the entry's list of overviews.
    /// </summary>
    /// <value>
    ///   <see cref="InlineTextPlacement.First"/> unless the entry's source
    ///   lists it among its other overviews.
    /// </value>
    InlineTextPlacement InlineOverviewPlacement { get => InlineTextPlacement.First; }
}

/// <summary>
///   Where an entry lists the default text kept on its row.
/// </summary>
internal enum InlineTextPlacement
{
    /// <summary>
    ///   Ahead of every stored text.
    /// </summary>
    First,

    /// <summary>
    ///   Where its source listed it: in the first gap in the positions of the
    ///   source's stored texts, or after them when there is none.
    /// </summary>
    InGap,

    /// <summary>
    ///   Not listed, as its source did not list it; it is still the default,
    ///   and answers <c>x-main</c> for its source, but no other language.
    /// </summary>
    Unlisted,
}
