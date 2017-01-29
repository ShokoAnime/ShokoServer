using Nancy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JMMContracts.PlexAndKodi;
using JMMServer.Entities;
using Response = Nancy.Response;

namespace JMMServer.API
{
    public static class NancyExtensions
    {
        public static Response FromByteArray(this IResponseFormatter formatter, byte[] body, string contentType = null)
        {
            return new ByteArrayResponse(body, contentType);
        }

        public static List<AnimeTitle> ToAPIContract(this List<AniDB_Anime_Title> titles)
        {
            List<AnimeTitle> result = new List<AnimeTitle>();
            titles.ForEach(a => result.Add(new AnimeTitle() { Title = a.Title, Language = a.Language, Type = a.TitleType }));
            return result;
        }
    }
}
