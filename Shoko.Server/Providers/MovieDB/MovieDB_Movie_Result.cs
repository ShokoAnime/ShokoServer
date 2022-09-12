using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using NLog;
using Shoko.Models.Client;
using Shoko.Models.Server;
using TMDbLib.Objects.General;
using TMDbLib.Objects.Movies;

namespace Shoko.Server.Providers.MovieDB
{
    public class MovieDB_Movie_Result : MovieDB_Movie
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public List<MovieDB_Image_Result> Images { get; set; }

        public override string ToString()
        {
            return "MovieDBSearchResult: " + MovieId + ": " + MovieName;
        }

        public bool Populate(Movie movie)
        {
            
            try
            {
                Blob = LZ4.CompressionHelper.SerializeObjectInclSize(movie);
                Images = new List<MovieDB_Image_Result>();
                MovieId = movie.Id;
                MovieName = movie.Title;
                OriginalName = movie.Title;
                Overview = movie.Overview;
                Rating = (int)Math.Round(movie.VoteAverage * 10D);
                MD5 = BitConverter.ToString(System.Security.Cryptography.MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(movie)))).Replace("-", "").ToLowerInvariant();
                if (movie.Images?.Backdrops != null)
                {
                    foreach (ImageData img in movie.Images.Backdrops)
                    {
                        MovieDB_Image_Result imageResult = new MovieDB_Image_Result();
                        if (imageResult.Populate(img, "backdrop"))
                            Images.Add(imageResult);
                    }
                }

                if (movie.Images?.Posters != null)
                {
                    foreach (ImageData img in movie.Images.Posters)
                    {
                        MovieDB_Image_Result imageResult = new MovieDB_Image_Result();
                        if (imageResult.Populate(img, "poster"))
                            Images.Add(imageResult);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return false;
            }

            return true;
        }

        public CL_MovieDBMovieSearch_Response ToContract()
        {
            CL_MovieDBMovieSearch_Response cl = new CL_MovieDBMovieSearch_Response
            {
                MovieID = MovieId,
                MovieName = MovieName,
                OriginalName = OriginalName,
                Overview = Overview
            };
            return cl;
        }
    }
}