#nullable enable
using System.Collections.Generic;
using Newtonsoft.Json;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Video.Release;

namespace Shoko.Server.Models.Release;

public class EmbeddedCrossReference : IReleaseVideoCrossReference
{
    public Dictionary<string, string> ProviderIDs { get; set; } = new();

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public int PercentageStart { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public int PercentageEnd { get; set; } = 100;

    /// <summary>
    /// Episode type (Episode, Special, Credits, etc.). Cached from AniDB at
    /// save time; not a provider-supplied ID.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public EpisodeType EpisodeType { get; set; } = EpisodeType.Episode;

    /// <summary>
    /// Episode number within its type. Cached from AniDB at save time.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public int EpisodeNumber { get; set; }

    public EmbeddedCrossReference() { }

    public EmbeddedCrossReference(IReleaseVideoCrossReference crossReference)
    {
        ProviderIDs = new Dictionary<string, string>(crossReference.ProviderIDs);
        PercentageStart = crossReference.PercentageStart;
        PercentageEnd = crossReference.PercentageEnd;
        if (crossReference is EmbeddedCrossReference embedded)
        {
            EpisodeType = embedded.EpisodeType;
            EpisodeNumber = embedded.EpisodeNumber;
        }
    }

    IReadOnlyDictionary<string, string> IReleaseVideoCrossReference.ProviderIDs => ProviderIDs;

    /// <summary>
    /// Returns the episode identifier in the standard Shoko format, e.g.
    /// <c>1</c> for episode 1, <c>S2</c> for special 2, <c>C1</c> for credits 1.
    /// </summary>
    public string ToEpisodeString()
    {
        var prefix = EpisodeType switch
        {
            EpisodeType.Special => "S",
            EpisodeType.Credits => "C",
            EpisodeType.Trailer => "T",
            EpisodeType.Parody => "P",
            EpisodeType.Other => "O",
            _ => string.Empty,
        };
        return prefix + EpisodeNumber;
    }
}
