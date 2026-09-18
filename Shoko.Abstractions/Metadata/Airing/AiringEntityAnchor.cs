using Shoko.Abstractions.Metadata.Shoko;

namespace Shoko.Abstractions.Metadata.Airing;

/// <summary>
///   Which entities a read is anchored to: the ones the providers actually
///   stored their schedules and airings against, or the shoko entities those
///   resolve to.
/// </summary>
/// <remarks>
///   <para>
///     A schedule is keyed on whatever entity its provider knew about — an
///     AniList anime, a TMDB show, a plugin's own series — and the same run can
///     therefore be reached from either side of a link. The anchor says which
///     side the caller wants back, which is also what decides whether an airing
///     nothing in the collection matches is part of the answer at all.
///   </para>
///   <para>
///     This is deliberately an enum with an <see cref="Auto"/> member rather
///     than a nullable one: <c>default</c> lands on inference, a
///     <c>switch</c> over it is checked by the compiler, and it serialises as a
///     self-documenting <c>"Auto"</c> rather than an absent field.
///   </para>
/// </remarks>
public enum AiringEntityAnchor
{
    /// <summary>
    ///   Infer the anchor from the entity the read was given: a shoko entity
    ///   anchors to <see cref="Shoko"/>, an AniList, TMDB or plugin entity
    ///   anchors to <see cref="Raw"/>. A read that takes no entity at all — a
    ///   range read, a read by schedule, provider or channel ID, or a
    ///   subscription — falls back to <see cref="Raw"/>.
    /// </summary>
    Auto = 0,

    /// <summary>
    ///   Answer with the provider's own entities, exactly as the schedules and
    ///   airings were stored. Nothing is dropped for having no counterpart in
    ///   the collection.
    /// </summary>
    Raw = 1,

    /// <summary>
    ///   Answer with the shoko entities the providers' own resolve to, and drop
    ///   whatever does not resolve to one: an airing with no
    ///   <see cref="IShokoEpisode"/> behind it, or a schedule with no
    ///   <see cref="IShokoSeries"/>, is not part of the answer.
    /// </summary>
    Shoko = 2,
}
