using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Extensions;
using Shoko.Server.Models;

namespace Shoko.Server.API.v3.Models.AniDB;

/// <summary>
/// AniDB specific data for an Episode
/// </summary>
public class AnidbEpisode
{
    /// <summary>
    /// AniDB Episode ID
    /// </summary>
    public int ID { get; set; }

    /// <summary>
    /// Episode Type
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public EpisodeType Type { get; set; }

    /// <summary>
    /// Episode Number
    /// </summary>
    public int EpisodeNumber { get; set; }

    /// <summary>
    /// First Listed Air Date. This may not be when it aired, but an early release date
    /// </summary>
    public DateOnly? AirDate { get; set; }

    /// <summary>
    /// Preferred title for the episode.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// All titles for the episode.
    /// </summary>
    public List<Title> Titles { get; set; }

    /// <summary>
    /// AniDB Episode Summary
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Episode Rating
    /// </summary>
    public Rating Rating { get; set; }

    public AnidbEpisode(SVR_AniDB_Episode ep)
    {
        if (!decimal.TryParse(ep.Rating, out var rating))
            rating = 0;
        if (!int.TryParse(ep.Votes, out var votes))
            votes = 0;

        var defaultTitle = ep.DefaultTitle;
        var mainTitle = ep.PreferredTitle;
        var titles = ep.GetTitles();
        ID = ep.EpisodeID;
        Type = ep.AbstractEpisodeType.ToV3Dto();
        EpisodeNumber = ep.EpisodeNumber;
        AirDate = ep.GetAirDateAsDate()?.ToDateOnly();
        Description = ep.Description;
        Rating = new Rating { MaxValue = 10, Value = rating, Votes = votes, Source = "AniDB" };
        Title = mainTitle.Title;
        Titles = titles
            .Select(a => new Title(a, defaultTitle.Title, mainTitle))
            .ToList();
    }
}
