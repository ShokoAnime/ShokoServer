using System.Runtime.Serialization;
using Microsoft.AspNetCore.Http;
using Shoko.Server.Models;

namespace Shoko.Server.API.v2.Models.common
{
    public class SearchResult : Serie
    {
        [DataMember]
        public string match { get; set; }

        public static SearchResult GenerateFromAnimeSeries(HttpContext ctx, SVR_AnimeSeries ser, int uid, bool nocast, bool notag,
            int level,
            bool all, string match, bool allpic, int pic, TagFilter.Filter tagfilter)
        {
            Serie serie = GenerateFromAnimeSeries(ctx, ser, uid, nocast, notag, level, all, allpic, pic, tagfilter);
            return new SearchResult(serie, match);
        }

        public SearchResult(Serie serie, string matched)
        {
            added = serie.added;
            air = serie.air;
            art = serie.art;
            edited = serie.edited;
            eps = serie.eps;
            id = serie.id;
            ismovie = serie.ismovie;
            localsize = serie.localsize;
            name = serie.name;
            rating = serie.rating;
            roles = serie.roles;
            season = serie.season;
            size = serie.size;
            summary = serie.summary;
            tags = serie.tags;
            titles = serie.titles;
            url = serie.url;
            userrating = serie.userrating;
            viewed = serie.viewed;
            year = serie.year;
            match = matched;
        }
    }
}