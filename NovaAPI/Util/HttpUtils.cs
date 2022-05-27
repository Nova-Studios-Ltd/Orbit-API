using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace NovaAPI.Util
{
    public static class HttpUtils
    {
        public static string GetToken(this ControllerBase controller)
        {
            if (!controller.HttpContext.Request.Headers.TryGetValue("Authorization", out StringValues values))
                return "";
            return values.First();
        }

        public async static Task<HttpWebResponse> GetResponseSilent(this WebRequest req)
        {
            try
            {
                return (HttpWebResponse) await req.GetResponseAsync();
            }
            catch (WebException we)
            {
                var resp = we.Response as HttpWebResponse;
                if (resp == null)
                    throw;
                return resp;
            }
        }
    }
}
