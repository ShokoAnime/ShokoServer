using System;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Shoko.Server.API.v3
{
    [ApiController]
    [Authorize]
    [Route("/api3/series")]
    public class SeriesController : BaseController
    {
        [HttpGet("{id}")]
        public ActionResult<Series> GetSeries(int id)
        {
            ParseQuery();
            return new Series(HttpContext, id);
        }


        private void ParseQuery()
        {
            var query = HttpContext.Request.Query;
            if (query.ContainsKey("include"))
            {
                var includeQuery = query["include"].ToList();
                if (includeQuery.Count > 1)
                {
                    if (includeQuery.Contains("all"))
                    {
                        HttpContext.Items.Add("cast", 0);
                        HttpContext.Items.Add("tags", 0);
                        HttpContext.Items.Add("descriptions", 0);
                        HttpContext.Items.Add("images", 0);
                        HttpContext.Items.Add("titles", 0);
                    }
                    else
                    {
                        if (includeQuery.Contains("cast")) HttpContext.Items.Add("cast", 0);
                        if (includeQuery.Contains("tags")) HttpContext.Items.Add("tags", 0);
                        if (includeQuery.Contains("description")) HttpContext.Items.Add("description", 0);
                        if (includeQuery.Contains("images")) HttpContext.Items.Add("images", 0);
                        if (includeQuery.Contains("titles")) HttpContext.Items.Add("titles", 0);
                    }
                }
                else
                {
                    // TODO Makes this more safe
                    includeQuery = includeQuery.FirstOrDefault()
                        .Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries).ToList();
                    if (includeQuery.Contains("all"))
                    {
                        HttpContext.Items.Add("cast", 0);
                        HttpContext.Items.Add("tags", 0);
                        HttpContext.Items.Add("descriptions", 0);
                        HttpContext.Items.Add("images", 0);
                    }
                    else
                    {
                        if (includeQuery.Contains("cast")) HttpContext.Items.Add("cast", 0);
                        if (includeQuery.Contains("tags")) HttpContext.Items.Add("tags", 0);
                        if (includeQuery.Contains("description")) HttpContext.Items.Add("description", 0);
                        if (includeQuery.Contains("images")) HttpContext.Items.Add("images", 0);
                    }
                }
            } else if (query.ContainsKey("tagfilter"))
            {
                // TODO Makes this more safe
                HttpContext.Items.Add("tagfilter", int.Parse(query["tagfilter"].FirstOrDefault()));
            }
        }
    }
}