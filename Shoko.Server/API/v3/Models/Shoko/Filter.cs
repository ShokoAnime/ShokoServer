using Microsoft.AspNetCore.Http;
using Shoko.Server.Models;

namespace Shoko.Server.API.v3
{
    public class Filter : BaseModel
    {


        public Filter(HttpContext ctx, SVR_GroupFilter gf)
        {
            GenerateFromGroupFilter(ctx, gf);
        }

        public void GenerateFromGroupFilter(HttpContext ctx, SVR_GroupFilter gf)
        {
            
        }
    }
}