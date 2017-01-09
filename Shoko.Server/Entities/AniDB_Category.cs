using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AniDBAPI;

namespace JMMServer.Entities
{
	public class AniDB_Category : IComparable<AniDB_Category>
	{
		public int AniDB_CategoryID { get; private set; }
		public int CategoryID { get; set; }
		public int ParentID { get; set; }
		public int IsHentai { get; set; }
		public string CategoryName { get; set; }
		public string CategoryDescription { get; set; }

		public void Populate(Raw_AniDB_Category rawCat)
		{
			this.CategoryID = rawCat.CategoryID;
			this.CategoryDescription = rawCat.CategoryDescription;
			this.CategoryName = rawCat.CategoryName;
			this.IsHentai = rawCat.IsHentai;
			this.ParentID = rawCat.ParentID;
		}

		public int CompareTo(AniDB_Category obj)
		{
			return CategoryName.CompareTo(obj.CategoryName);
		}

		public override string ToString()
		{
			return string.Format("AniDB_Category: {0}({1}) - {2}", CategoryName, CategoryID, CategoryDescription);
		}
	}
}
