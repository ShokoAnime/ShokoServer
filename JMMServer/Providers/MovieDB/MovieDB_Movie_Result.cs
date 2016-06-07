using System;
using System.Collections.Generic;
using JMMContracts;
using NLog;
using TMDbLib.Objects.General;
using TMDbLib.Objects.Movies;

namespace JMMServer.Providers.MovieDB
{
    public class MovieDB_Movie_Result
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public int MovieID { get; set; }
        public string MovieName { get; set; }
        public string OriginalName { get; set; }
        public string Overview { get; set; }

        public List<MovieDB_Image_Result> Images { get; set; }

        public override string ToString()
        {
            return "MovieDBSearchResult: " + MovieID + ": " + MovieName;
        }

        public bool Populate(Movie movie, ImagesWithId imgs)
        {
            try
            {
                Images = new List<MovieDB_Image_Result>();

                MovieID = movie.Id;
                MovieName = movie.Title;
                OriginalName = movie.Title;
                Overview = movie.Overview;

                if (imgs != null && imgs.Backdrops != null)
                {
                    foreach (var img in imgs.Backdrops)
                    {
                        var imageResult = new MovieDB_Image_Result();
                        if (imageResult.Populate(img, "backdrop"))
                            Images.Add(imageResult);
                    }
                }

                if (imgs != null && imgs.Posters != null)
                {
                    foreach (var img in imgs.Posters)
                    {
                        var imageResult = new MovieDB_Image_Result();
                        if (imageResult.Populate(img, "poster"))
                            Images.Add(imageResult);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return false;
            }

            return true;
        }

        public Contract_MovieDBMovieSearchResult ToContract()
        {
            var contract = new Contract_MovieDBMovieSearchResult();
            contract.MovieID = MovieID;
            contract.MovieName = MovieName;
            contract.OriginalName = OriginalName;
            contract.Overview = Overview;
            return contract;
        }
    }
}