
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Release;

namespace Shoko.Server.API.v3.Models.Release;

public class ReleaseMediaInfo : IReleaseMediaInfo
{
    /// <summary>
    /// Gets the audio languages associated with the media file.
    /// </summary>
    [Required]
    [JsonProperty(ItemConverterType = typeof(StringEnumConverter))]
    public IReadOnlyList<TitleLanguage> AudioLanguages { get; init; }

    /// <summary>
    /// Gets the subtitle languages associated with the media file.
    /// </summary>
    [Required]
    [JsonProperty(ItemConverterType = typeof(StringEnumConverter))]
    public IReadOnlyList<TitleLanguage> SubtitleLanguages { get; init; }

    public ReleaseMediaInfo() { }

    public ReleaseMediaInfo(IReleaseMediaInfo mediaInfo)
    {
        AudioLanguages = mediaInfo.AudioLanguages;
        SubtitleLanguages = mediaInfo.SubtitleLanguages;
    }
}
