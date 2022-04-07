using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
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
    }
}
