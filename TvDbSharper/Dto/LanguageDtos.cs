namespace TvDbSharper.Dto
{
    public class Language
    {
        /// <summary>
        /// The language abbreviation that can be used as a value for the <see cref="T:ITvDbClient.AcceptedLanguage"/> property.
        /// </summary>
        public string Abbreviation { get; set; }

        /// <summary>
        /// The name of the language in english
        /// </summary>
        public string EnglishName { get; set; }

        /// <summary>
        /// The language ID
        /// </summary>
        public int? Id { get; set; }

        /// <summary>
        /// The name of the language in it's native form
        /// </summary>
        public string Name { get; set; }
    }
}