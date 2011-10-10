using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

namespace JMMServer.Providers.MovieDB
{
	public class MovieDB_Image_Result
	{
		public string ImageID { get; set; }
		public string ImageType { get; set; }
		public string ImageSize { get; set; }
		public string URL { get; set; }
		public int ImageWidth { get; set; }
		public int ImageHeight { get; set; }

		public MovieDB_Image_Result()
		{
		}

		public override string ToString()
		{
			return string.Format("{0} - {1} - {2}x{3} - {4}", ImageType, ImageSize, ImageWidth, ImageHeight, URL);
		}

		public bool Populate(XmlNode result)
		{
			if (result.Attributes["id"] == null) return false;

			ImageType = string.Empty;
			ImageSize = string.Empty;
			URL = string.Empty;
			ImageWidth = 0;
			ImageHeight = 0;

			if (result.Attributes["id"] != null) ImageID = result.Attributes["id"].InnerText;
			if (result.Attributes["type"] != null) ImageType = result.Attributes["type"].InnerText;
			if (result.Attributes["size"] != null) ImageSize = result.Attributes["size"].InnerText;
			if (result.Attributes["url"] != null) URL = result.Attributes["url"].InnerText;
			if (result.Attributes["width"] != null) ImageWidth = int.Parse(result.Attributes["width"].InnerText);
			if (result.Attributes["height"] != null) ImageHeight = int.Parse(result.Attributes["height"].InnerText);

			return true;
		}
	}
}
