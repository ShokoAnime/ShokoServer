using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Shoko;

namespace Shoko.Server.API.v3.Controllers
{
    [ApiController, Route("/api/v{version:apiVersion}/Utility/FileRelocation"), ApiV3]
    [Authorize]
    public class FileRelocationController : BaseController
    {
        /// <summary>
        /// Get all available <see cref="FileRelocationPipeline"/>s.
        /// </summary>
        /// <returns></returns>
        [HttpGet("Pipeline")]
        public ActionResult<List<FileRelocationPipeline>> GetAllPipelines()
        {
            return new List<FileRelocationPipeline>();
        }

        /// <summary>
        /// Create a new pipeline.
        /// </summary>
        /// <param name="body"></param>
        /// <returns></returns>
        [HttpPost("Pipeline")]
        public ActionResult AddPipeline([FromBody] FileRelocationPipeline body)
        {
            return Created("1", null);
        }

        /// <summary>
        /// View an existing pipeline.
        /// </summary>
        /// <param name="pipelineID"></param>
        /// <returns></returns>
        [HttpGet("Pipeline/{pipelineID}")]
        public ActionResult<FileRelocationPipeline> GetPipelineByPipelineID([FromRoute] int pipelineID)
        {
            return null;
        }

        /// <summary>
        /// Edit an existing pipeline.
        /// </summary>
        /// <param name="pipelineID"></param>
        /// <param name="body"></param>
        /// <returns></returns>
        [HttpPut("Pipeline/{pipelineID}")]
        public ActionResult<FileRelocationPipeline> DeletePipelineByPipelineID([FromRoute] int pipelineID, [FromBody] FileRelocationPipeline body)
        {
            return null;
        }

        /// <summary>
        /// Delete an existing pipeline.
        /// </summary>
        /// <param name="pipelineID"></param>
        /// <returns></returns>
        [HttpDelete("Pipeline/{pipelineID}")]
        public ActionResult RemovePipelineByPipelineID([FromRoute] int pipelineID)
        {
            return Ok();
        }
    }
}