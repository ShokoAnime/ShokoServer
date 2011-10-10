using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using NLog;
using JMMContracts;

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

		public bool Populate(XmlNode result)
		{
			if (result["id"] == null) return false;
			try
			{
				MovieName = string.Empty;
				OriginalName = string.Empty;
				Overview = string.Empty;
				Images = new List<MovieDB_Image_Result>();

				if (result["id"] != null) MovieID = int.Parse(result["id"].InnerText);
				if (result["name"] != null) MovieName = result["name"].InnerText;
				if (result["original_name"] != null) OriginalName = result["original_name"].InnerText;
				if (result["overview"] != null) Overview = result["overview"].InnerText;

				//XmlNodeList imgs = result.SelectNodes("image");
				XmlNodeList imgs = result["images"].GetElementsByTagName("image");

				foreach (XmlNode img in imgs)
				{
					MovieDB_Image_Result imageResult = new MovieDB_Image_Result();
					if (imageResult.Populate(img))
						Images.Add(imageResult);

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
