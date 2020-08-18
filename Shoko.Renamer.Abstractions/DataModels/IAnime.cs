using System;
using System.Collections.Generic;

namespace Shoko.Renamer.Abstractions.DataModels
{
    public interface IAnime
    {
        int AnimeID { get; }
        int EpisodeCount { get; }
        DateTime? AirDate { get; }
        DateTime? EndDate { get; }
        AnimeType Type { get; }
        IList<AnimeTitle> Titles { get; }
        double Rating { get; }
    }

    public class AnimeTitle
    {
        public string Language { get; set; }
        public string Title { get; set; }
        public TitleType Type { get; set; }
    }

    public enum AnimeType
    {
        Movie = 0,
        OVA = 1,
        TVSeries = 2,
        TVSpecial = 3,
        Web = 4,
        Other = 5
    }

    public enum TitleType
    {
        None = 0,
        Main = 1,
        Official = 2,
        Short = 3,
        Synonym = 4
    }
}