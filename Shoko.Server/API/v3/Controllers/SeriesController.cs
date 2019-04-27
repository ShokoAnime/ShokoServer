
using System;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Server.API.Annotations;

namespace Shoko.Server.API.v3
{
    [ApiController, Route("/api/{version:apiVersion}/[controller]"), ApiV3]
    [Authorize]
    public class SeriesController : BaseController
    {
        [HttpGet("{id}")]
        public ActionResult<Series> GetSeries(int id)
        {
            return new Series(HttpContext, id);
        }
    }
}
