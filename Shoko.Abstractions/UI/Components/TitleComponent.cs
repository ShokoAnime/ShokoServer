namespace Shoko.Abstractions.UI.Components;

/// <summary>
///   What a row calls itself.
/// </summary>
/// <remarks>
///   <para>
///     A list of classes renders one row per entry, and a row needs a label. A
///     class holding one of these says what to put there, and the list element
///     points at it: <see cref="Title"/> becomes the row's title and
///     <see cref="SubTitle"/> the line under it, so neither has to be guessed
///     from the names of the class's other members.
///   </para>
///   <para>
///     A class without one falls back to the member marked <c>[Key]</c>, and a
///     record entry with neither falls back to the key it is stored under.
///   </para>
/// </remarks>
public class TitleComponent
{
    /// <summary>
    ///   The row's title.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    ///   The line under the title, for whatever tells two similar rows apart.
    /// </summary>
    public string? SubTitle { get; set; }

    /// <summary>
    ///   Initializes a new instance of the <see cref="TitleComponent"/> class.
    /// </summary>
    public TitleComponent() { }

    /// <summary>
    ///   Initializes a new instance of the <see cref="TitleComponent"/> class.
    /// </summary>
    /// <param name="title">The row's title.</param>
    /// <param name="subTitle">The line under it.</param>
    public TitleComponent(string? title, string? subTitle = null)
    {
        Title = title;
        SubTitle = subTitle;
    }
}
