using System;
using System.Collections.Generic;
using NLog;
using Shoko.Models.Client;
using Shoko.Models.Server;
using TMDbLib.Objects.General;
using TMDbLib.Objects.TvShows;

namespace Shoko.Server.Providers.MovieDB
{
    public class MovieDB_Series_Result : MovieDB_Series
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public List<MovieDB_Image_Result> Images { get; set; }

        public override string ToString()
        {
            return "MovieDBTVSeriesSearchResult: " + SeriesID + ": " + SeriesName;
        }

        public bool Populate(TvShow show)
        {
            try
            {
                Blob = LZ4.CompressionHelper.SerializeObject(show, out int _);
                Images = new List<MovieDB_Image_Result>();
                SeriesID = show.Id;
                SeriesName = show.Name;
                OriginalName = show.OriginalName;
                Overview = show.Overview;
                Rating = (int)Math.Round(show.VoteAverage * 10D);
                Lastupdated = DateTimeOffset.UtcNow.ToString("o");
                if (show.Images?.Backdrops != null)
                {
                    foreach (ImageData img in show.Images.Backdrops)
                    {
                        MovieDB_Image_Result imageResult = new MovieDB_Image_Result();
                        if (imageResult.Populate(img, "backdrop"))
                            Images.Add(imageResult);
                    }
                }

                if (show.Images?.Posters != null)
                {
                    foreach (ImageData img in show.Images.Posters)
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

        public CL_MovieDBTVSeriesSearch_Response ToContract()
        {
            CL_MovieDBTVSeriesSearch_Response cl = new CL_MovieDBTVSeriesSearch_Response
            {
                SeriesID = SeriesID,
                SeriesName = SeriesName,
                OriginalName = OriginalName,
                Overview = Overview
            };
            return cl;
        }
    }
}
