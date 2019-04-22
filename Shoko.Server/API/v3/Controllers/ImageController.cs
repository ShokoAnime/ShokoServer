using Microsoft.AspNetCore.Mvc;
using Shoko.Models.Enums;
using Shoko.Server.API.Annotations;
using Mime = MimeMapping.MimeUtility;

namespace Shoko.Server.API.v3
{
    [ApiController, Route("/api/{version:apiVersion}/[controller]"), ApiV3]
    public class ImageController : BaseController
    {
        
        /// <summary>
        /// /apiv3/image/tvdb/fanart/12
        /// returns an image
        /// </summary>
        /// <param name="source"></param>
        /// <param name="type"></param>
        /// <param name="id"></param>
        /// <returns>200 on found, 400 if the type or source are invalid, and 404 if the id is not found</returns>
        [HttpGet("{source}/{type}/{id}")]
        [ProducesResponseType(typeof(FileStreamResult),200), ProducesResponseType(400), ProducesResponseType(404)]
        public ActionResult GetImage(string source, string type, int id)
        {
            source = source.ToLower();
            type = type.ToLower();
            ImageEntityType? sourceType = Image.GetImageTypeFromSourceAndType(source, type);

            if (sourceType == null) return BadRequest();
            string path = Image.GetImagePath(sourceType.Value, id);
            if (string.IsNullOrEmpty(path)) return NotFound();
            return File(System.IO.File.OpenRead(path), Mime.GetMimeMapping(path));
        }
    }
}