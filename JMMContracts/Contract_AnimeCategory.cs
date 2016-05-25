namespace JMMContracts
{
	public class Contract_AnimeCategory
	{
		public int CategoryID { get; set; }
		public int ParentID { get; set; }
		public int IsHentai { get; set; }
		public string CategoryName { get; set; }
		public string CategoryDescription { get; set; }
		public int Weighting { get; set; }
	}
}
