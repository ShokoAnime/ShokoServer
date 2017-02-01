using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;

namespace Shoko.Commons.Downloads
{
    public class CloudflareEvader
    {
        /// <summary>
        /// Tries to return a webclient with the neccessary cookies installed to do requests for a cloudflare protected website.
        /// </summary>
        /// <param name="url">The page which is behind cloudflare's anti-dDoS protection</param>
        /// <returns>A WebClient object or null on failure</returns>
        public static WebClientEx CreateBypassedWebClient(string url)
        {
            var JSEngine = new Jint.Engine(); //Use this JavaScript engine to compute the result.

            //Download the original page
            var uri = new Uri(url);
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; WOW64; rv:40.0) Gecko/20100101 Firefox/40.0";
            //Try to make the usual request first. If this fails with a 503, the page is behind cloudflare.
            try
            {
                var res = req.GetResponse();
                string html = "";
                using (var reader = new StreamReader(res.GetResponseStream()))
                    html = reader.ReadToEnd();
                return new WebClientEx();
            }
            catch (WebException ex) //We usually get this because of a 503 service not available.
            {
                string html = "";
                using (var reader = new StreamReader(ex.Response.GetResponseStream()))
                    html = reader.ReadToEnd();
                //If we get on the landing page, Cloudflare gives us a User-ID token with the cookie. We need to save that and use it in the next request.
                var cookie_container = new CookieContainer();
                //using a custom function because ex.Response.Cookies returns an empty set ALTHOUGH cookies were sent back.
                var initial_cookies = GetAllCookiesFromHeader(ex.Response.Headers["Set-Cookie"], uri.Host);
                foreach (Cookie init_cookie in initial_cookies)
                    cookie_container.Add(init_cookie);

                /* solve the actual challenge with a bunch of RegEx's. Copy-Pasted from the python scrapper version.*/
                var challenge = Regex.Match(html, "name=\"jschl_vc\" value=\"(\\w+)\"").Groups[1].Value;
                var challenge_pass = Regex.Match(html, "name=\"pass\" value=\"(.+?)\"").Groups[1].Value;

                Match match = Regex.Match(html, @"setTimeout\(function\(\){\s+(var t,r,a,f.+?\r?\n[\s\S]+?a\.value =.+?)\r?\n");
                string builder=string.Empty;
                if (match.Success)
                {
                    builder = match.Groups[1].Value;
                    builder = Regex.Replace(builder, @"a\.value =(.+?) \+ .+?;", "$1");
                }
                else
                {
                    match = Regex.Match(html, @"setTimeout\(function\(\){\s+(var s,t,o,p,b,r,e,a,k,i,n,g,f.+?\r?\n[\s\S]+?a\.value =.+?)\r?\n");
                    if (match.Success)
                    {
                        builder = match.Groups[1].Value;
                        builder = Regex.Replace(builder, @"a\.value = (parseInt\(.+?\)).+", "$1");
                    }
                }
                builder = Regex.Replace(builder, @"\s{3,}[a-z](?: = |\.).+", "");

                //Format the javascript..
                builder = Regex.Replace(builder, @"[\n\\']", "");

                //Execute it. 
                long solved = long.Parse(JSEngine.Execute(builder).GetCompletionValue().ToObject().ToString());
                solved += uri.Host.Length; //add the length of the domain to it.

                Console.WriteLine("***** SOLVED CHALLENGE ******: " + solved);
                Thread.Sleep(3000); //This sleeping IS requiered or cloudflare will not give you the token!!

                //Retreive the cookies. Prepare the URL for cookie exfiltration.
                string cookie_url = $"{uri.Scheme}://{uri.Host}/cdn-cgi/l/chk_jschl";
                var uri_builder = new UriBuilder(cookie_url);
                var query = HttpUtility.ParseQueryString(uri_builder.Query);
                //Add our answers to the GET query
                query["jschl_vc"] = challenge;
                query["jschl_answer"] = solved.ToString();
                query["pass"] = challenge_pass;
                uri_builder.Query = query.ToString();

                //Create the actual request to get the security clearance cookie
                HttpWebRequest cookie_req = (HttpWebRequest)WebRequest.Create(uri_builder.Uri);
                cookie_req.AllowAutoRedirect = false;
                cookie_req.CookieContainer = cookie_container;
                cookie_req.Referer = url;
                cookie_req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; WOW64; rv:40.0) Gecko/20100101 Firefox/40.0";
                //We assume that this request goes through well, so no try-catch
                var cookie_resp = (HttpWebResponse)cookie_req.GetResponse();
                //The response *should* contain the security clearance cookie!
                if (cookie_resp.Cookies.Count != 0) //first check if the HttpWebResponse has picked up the cookie.
                    foreach (Cookie cookie in cookie_resp.Cookies)
                        cookie_container.Add(cookie);
                else //otherwise, use the custom function again
                {
                    //the cookie we *hopefully* received here is the cloudflare security clearance token.
                    if (cookie_resp.Headers["Set-Cookie"] != null)
                    {
                        var cookies_parsed = GetAllCookiesFromHeader(cookie_resp.Headers["Set-Cookie"], uri.Host);
                        foreach (Cookie cookie in cookies_parsed)
                            cookie_container.Add(cookie);
                    }
                    else
                    {
                        //No security clearence? something went wrong.. return null.
                        //Console.WriteLine("MASSIVE ERROR: COULDN'T GET CLOUDFLARE CLEARANCE!");
                        return null;
                    }
                }
                //Create a custom webclient with the two cookies we already acquired.
                WebClientEx modedWebClient = new WebClientEx(cookie_container);
                modedWebClient.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; WOW64; rv:40.0) Gecko/20100101 Firefox/40.0");
                modedWebClient.Headers.Add("Referer", url);
                return modedWebClient;
            }
        }

        /* Credit goes to https://stackoverflow.com/questions/15103513/httpwebresponse-cookies-empty-despite-set-cookie-header-no-redirect 
           (user https://stackoverflow.com/users/541404/cameron-tinker) for these functions 
        */
        public static CookieCollection GetAllCookiesFromHeader(string strHeader, string strHost)
        {
            ArrayList al = new ArrayList();
            CookieCollection cc = new CookieCollection();
            if (strHeader != string.Empty)
            {
                al = ConvertCookieHeaderToArrayList(strHeader);
                cc = ConvertCookieArraysToCookieCollection(al, strHost);
            }
            return cc;
        }

        private static ArrayList ConvertCookieHeaderToArrayList(string strCookHeader)
        {
            strCookHeader = strCookHeader.Replace("\r", "");
            strCookHeader = strCookHeader.Replace("\n", "");
            string[] strCookTemp = strCookHeader.Split(',');
            ArrayList al = new ArrayList();
            int i = 0;
            int n = strCookTemp.Length;
            while (i < n)
            {
                if (strCookTemp[i].IndexOf("expires=", StringComparison.OrdinalIgnoreCase) > 0)
                {
                    al.Add(strCookTemp[i] + "," + strCookTemp[i + 1]);
                    i = i + 1;
                }
                else
                    al.Add(strCookTemp[i]);
                i = i + 1;
            }
            return al;
        }

        private static CookieCollection ConvertCookieArraysToCookieCollection(ArrayList al, string strHost)
        {
            CookieCollection cc = new CookieCollection();

            int alcount = al.Count;
            string strEachCook;
            string[] strEachCookParts;
            for (int i = 0; i < alcount; i++)
            {
                strEachCook = al[i].ToString();
                strEachCookParts = strEachCook.Split(';');
                int intEachCookPartsCount = strEachCookParts.Length;
                string strCNameAndCValue = string.Empty;
                string strPNameAndPValue = string.Empty;
                string strDNameAndDValue = string.Empty;
                string[] NameValuePairTemp;
                Cookie cookTemp = new Cookie();

                for (int j = 0; j < intEachCookPartsCount; j++)
                {
                    if (j == 0)
                    {
                        strCNameAndCValue = strEachCookParts[j];
                        if (strCNameAndCValue != string.Empty)
                        {
                            int firstEqual = strCNameAndCValue.IndexOf("=");
                            string firstName = strCNameAndCValue.Substring(0, firstEqual);
                            string allValue = strCNameAndCValue.Substring(firstEqual + 1, strCNameAndCValue.Length - (firstEqual + 1));
                            cookTemp.Name = firstName;
                            cookTemp.Value = allValue;
                        }
                        continue;
                    }
                    if (strEachCookParts[j].IndexOf("path", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        strPNameAndPValue = strEachCookParts[j];
                        if (strPNameAndPValue != string.Empty)
                        {
                            NameValuePairTemp = strPNameAndPValue.Split('=');
                            if (NameValuePairTemp[1] != string.Empty)
                                cookTemp.Path = NameValuePairTemp[1];
                            else
                                cookTemp.Path = "/";
                        }
                        continue;
                    }

                    if (strEachCookParts[j].IndexOf("domain", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        strPNameAndPValue = strEachCookParts[j];
                        if (strPNameAndPValue != string.Empty)
                        {
                            NameValuePairTemp = strPNameAndPValue.Split('=');

                            if (NameValuePairTemp[1] != string.Empty)
                                cookTemp.Domain = NameValuePairTemp[1];
                            else
                                cookTemp.Domain = strHost;
                        }
                        continue;
                    }
                }

                if (cookTemp.Path == string.Empty)
                    cookTemp.Path = "/";
                if (cookTemp.Domain == string.Empty)
                    cookTemp.Domain = strHost;
                cc.Add(cookTemp);
            }
            return cc;
        }
    }

}
