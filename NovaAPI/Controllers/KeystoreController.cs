using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace NovaAPI.Controllers
{
    [ApiController]
    public class KeystoreController : ControllerBase
    {
        [HttpGet("/User/@me/Keystore/T")]
        public ActionResult Test()
        {
            return StatusCode(200);
        }
    }
}