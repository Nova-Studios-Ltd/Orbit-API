using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MySql.Data.MySqlClient;
using NovaAPI.Controllers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NovaAPI.Util;

namespace NovaAPI.Attri
{
    public class TokenAuthorization : Attribute, IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            if (!context.HttpContext.Request.Headers.ContainsKey("Authorization"))
            {
                context.ModelState.AddModelError("Unauthorized", "Missing Authorization");
                context.Result = new UnauthorizedObjectResult(context.ModelState);
                return;
            }

            string token = context.HttpContext.Request.Headers.First(x => x.Key == "Authorization").Value;

            using MySqlConnection conn = MySqlServer.CreateSQLConnection(Database.Master);
            conn.Open();
            MySqlCommand cmd = new($"SELECT * FROM Users WHERE (Token=@token)", conn);
            cmd.Parameters.AddWithValue("@token", token);
            MySqlDataReader reader = cmd.ExecuteReader();
            if (!reader.HasRows)
            {
                context.ModelState.AddModelError("Unauthorized", "Invalid/Missing Token");
                context.Result = new UnauthorizedObjectResult(context.ModelState);
            }
        }
    }
}
