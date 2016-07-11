using System;

namespace JMMServer.API
{
    //class will be found automagicly thanks to inherits also class need to be public (error404)
    public class API_General: Nancy.NancyModule
    {
        public API_General()
        {
            Get["/"] = parameter => { return IndexPage; };
            Get["/version"] = parameter => { return System.Windows.Forms.Application.ProductVersion; };
        }

        const String IndexPage = @"<html><body><h1>JMMServer is running</h1></body></html>";
    }
}
