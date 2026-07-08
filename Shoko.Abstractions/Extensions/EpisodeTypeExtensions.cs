using System;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Extensions;

/// <summary>
/// Extensions for the <see cref="EpisodeType"/> enum.
/// </summary>
public static class EpisodeTypeExtensions
{
    extension(EpisodeType type)
    {
        /// <summary>
        ///   Get a string prefix for the episode type.
        /// </summary>
        public string Prefix => type switch
        {
            EpisodeType.Episode => "",
            EpisodeType.Credits => "C",
            EpisodeType.Special => "S",
            EpisodeType.Trailer => "T",
            EpisodeType.Parody => "P",
            EpisodeType.Other => "O",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown episode type."),
        };

        /// <summary>
        ///   Get the episode type from a prefix.
        /// </summary>
        /// <param name="prefix">
        ///   The prefix to get the episode type from.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        ///   Unknown episode number prefix.
        /// </exception>
        /// <returns>
        ///   The episode type.
        /// </returns>
        public static EpisodeType FromPrefix(string prefix)
            => prefix switch
            {
                "" => EpisodeType.Episode,
                "S" => EpisodeType.Special,
                "C" => EpisodeType.Credits,
                "T" => EpisodeType.Trailer,
                "P" => EpisodeType.Parody,
                "O" => EpisodeType.Other,
                _ => throw new ArgumentOutOfRangeException(nameof(prefix), prefix, "Unknown episode number prefix."),
            };
    }
}
