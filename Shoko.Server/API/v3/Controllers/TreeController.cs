using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Server.API.Annotations;

namespace Shoko.Server.API.v3
{
    /// <summary>
    /// This Controller is intended to provide the tree. An example would be "api/v3/filter/4/group/12/series".
    /// This is to support filtering with Apply At Series Level and any other situations that might involve the need for it.
    /// </summary>
    [ApiController, Route("/api/v{version:apiVersion}"), ApiV3]
    [Authorize]
    public class TreeController : BaseController
    {
        
    }
}