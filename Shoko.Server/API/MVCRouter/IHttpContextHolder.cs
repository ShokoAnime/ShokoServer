using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Text;

namespace Shoko.Server.API.MVCRouter
{
    interface IHttpContextHolder
    {
        HttpContext Context { get; set; }
    }
}
