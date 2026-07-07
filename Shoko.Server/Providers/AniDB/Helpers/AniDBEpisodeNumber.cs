using System;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Server.Providers.AniDB.Helpers;

public class AniDBEpisodeNumber
{
    /// <summary>
    /// The episode type, special,etc
    /// </summary>
    public EpisodeType EpisodeType { get; set; }

    /// <summary>
    /// Episode Number
    /// </summary>
    public int EpisodeNumber { get; set; }

    /// <summary>
    ///   Parses an AniDB episode code (e.g. "1", "S1", "C2") into
    ///   its type and number.
    /// </summary>
    /// <param name="value">
    ///   The AniDB episode code to parse.
    /// </param>
    public static AniDBEpisodeNumber Parse(string value)
    {
        var prefix = char.IsDigit(value[0]) ? string.Empty : value[..1];
        var type = prefix switch
        {
            "" => EpisodeType.Episode,
            "S" => EpisodeType.Special,
            "C" => EpisodeType.Credits,
            "T" => EpisodeType.Trailer,
            "P" => EpisodeType.Parody,
            "O" => EpisodeType.Other,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown episode number prefix."),
        };
        return new AniDBEpisodeNumber { EpisodeType = type, EpisodeNumber = int.Parse(value[prefix.Length..]) };
    }

    public override string ToString()
    {
        var prefix = EpisodeType switch
        {
            EpisodeType.Episode => string.Empty,
            EpisodeType.Special => "S",
            EpisodeType.Credits => "C",
            EpisodeType.Trailer => "T",
            EpisodeType.Parody => "P",
            EpisodeType.Other => "O",
            _ => throw new ArgumentOutOfRangeException(),
        };
        return prefix + EpisodeNumber;
    }
}
