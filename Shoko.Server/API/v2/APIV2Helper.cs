using Microsoft.AspNetCore.Http;

namespace Shoko.Server.API.v2;

public class APIV2Helper
{
    public static string ConstructUnsortUrl(HttpContext ctx, bool short_url = false)
    {
        return APIHelper.ProperURL(ctx, "/api/file/unsort", short_url);
    }

    public static string ConstructFilterIdUrl(HttpContext ctx, int groupfilter_id, bool short_url = false)
    {
        return APIHelper.ProperURL(ctx, "/api/filter?id=" + groupfilter_id, short_url);
    }

    public static string ConstructFilterUrl(HttpContext ctx, bool short_url = false)
    {
        return APIHelper.ProperURL(ctx, "/api/filter", short_url);
    }

    public static string ConstructVideoLocalStream(HttpContext ctx, int userid, string vid, string name, bool autowatch)
    {
        return APIHelper.ProperURL(ctx, "/Stream/" + vid + "/" + userid + "/" + autowatch + "/" + name);
    }

    public static string ConstructSupportImageLink(HttpContext ctx, string name, bool short_url = true)
    {
        return APIHelper.ProperURL(ctx, "/api/v2/image/support/" + name, short_url);
    }
}
