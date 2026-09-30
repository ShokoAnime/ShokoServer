using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Extensions;

/// <summary>
///   Turns a <see cref="MatchRating"/> into something you can sort and group
///   by.
/// </summary>
/// <remarks>
///   The enum's own values are storage ids, assigned as members were added and
///   once remapped, so they say nothing about how much to trust a match. Rank
///   through here instead.
/// </remarks>
public static class MatchRatingExtensions
{
    extension(MatchRating rating)
    {
        /// <summary>
        ///   How much to trust the match, higher being better and zero being
        ///   nothing.
        /// </summary>
        /// <remarks>
        ///   The spacing is deliberate: it leaves room to place a new rating
        ///   between two existing ones without renumbering everything, the way
        ///   the enum could not.
        /// </remarks>
        public int Score
            => rating switch
            {
                // A person said so, which outranks any amount of evidence.
                MatchRating.UserVerified => 100,

                // A title agreeing beats a number agreeing: numbers line up by
                // chance far more often.
                MatchRating.DateAndTitleMatches => 90,
                MatchRating.DateAndNumberMatches => 80,

                // A date match corroborated by an approximate title, not two weak
                // signals.
                MatchRating.DateAndTitleKindaMatches => 70,

                MatchRating.DateMatches => 60,
                MatchRating.TitleMatches => 50,

                MatchRating.TitleKindaMatches => 40,

                // Placed by its neighbours, which beats reaching for the nearest
                // date.
                MatchRating.DateOffsetMatches => 30,
                MatchRating.DateKindaMatches => 20,

                // Nothing matched; filled in to keep the run whole.
                MatchRating.FirstAvailable => 10,

                _ => 0,
            };

        /// <summary>
        ///   How much to trust the match, as a band rather than a number, for
        ///   showing a user.
        /// </summary>
        public MatchConfidence Confidence
            => rating.Score switch
            {
                >= 100 => MatchConfidence.Verified,
                // Two signals agreeing, however exactly.
                >= 70 => MatchConfidence.Strong,
                >= 50 => MatchConfidence.Probable,
                >= 40 => MatchConfidence.Weak,
                >= 20 => MatchConfidence.Inferred,
                >= 10 => MatchConfidence.Guessed,
                _ => MatchConfidence.None,
            };
    }
}
