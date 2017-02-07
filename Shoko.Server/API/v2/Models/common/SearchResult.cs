using System.Runtime.Serialization;
using Shoko.Server.Models;

namespace Shoko.Server.API.v2.Models.common
{
    public class SearchResult : Serie
    {
        [DataMember]
        public string match { get; set; }

        public static SearchResult GenerateFromAnimeSeries(SVR_AnimeSeries ser, int uid, bool nocast, bool notag, int level,
            bool all, string match)
        {
            Serie serie = Serie.GenerateFromAnimeSeries(ser, uid, nocast, notag, level, all);
            return new SearchResult(serie, match);
        }

        public SearchResult(Serie serie, string matched)
        {
            this.added = serie.added;
            this.air = serie.air;
            this.art = serie.art;
            this.edited = serie.edited;
            this.eps = serie.eps;
            this.id = serie.id;
            this.ismovie = serie.ismovie;
            this.localsize = serie.localsize;
            this.name = serie.name;
            this.rating = serie.rating;
            this.roles = serie.roles;
            this.season = serie.season;
            this.size = serie.size;
            this.summary = serie.summary;
            this.tags = serie.tags;
            this.titles = serie.titles;
            this.url = serie.url;
            this.userrating = serie.userrating;
            this.viewed = serie.viewed;
            this.year = serie.year;
            this.match = matched;
        }

    }
}