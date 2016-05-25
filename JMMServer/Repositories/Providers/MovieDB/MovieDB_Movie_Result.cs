﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using NLog;
using JMMContracts;
using TMDbLib.Objects.Search;
using TMDbLib.Objects.Movies;
using TMDbLib.Objects.General;

namespace JMMServer.Providers.MovieDB
{
	public class MovieDB_Movie_Result
	{
		private static Logger logger = LogManager.GetCurrentClassLogger();

		public int MovieID { get; set;}
		public string MovieName { get; set;}
		public string OriginalName { get; set;}
		public string Overview { get; set;}

		public List<MovieDB_Image_Result> Images { get; set; }

		public override string ToString()
		{
			return "MovieDBSearchResult: " + MovieID + ": " + MovieName;

		}

		public MovieDB_Movie_Result()
		{
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
                    foreach (ImageData img in imgs.Backdrops)
                    {
                        MovieDB_Image_Result imageResult = new MovieDB_Image_Result();
                        if (imageResult.Populate(img, "backdrop"))
                            Images.Add(imageResult);
                    }
                }

                if (imgs != null && imgs.Posters != null)
                {
                    foreach (ImageData img in imgs.Posters)
                    {
                        MovieDB_Image_Result imageResult = new MovieDB_Image_Result();
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
			Contract_MovieDBMovieSearchResult contract = new Contract_MovieDBMovieSearchResult();
			contract.MovieID = this.MovieID;
			contract.MovieName = this.MovieName;
			contract.OriginalName = this.OriginalName;
			contract.Overview = this.Overview;
			return contract;
		}
	}
}
