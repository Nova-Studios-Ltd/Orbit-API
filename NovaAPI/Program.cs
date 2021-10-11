using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace NovaAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    #if !DEBUG
                    webBuilder.UseKestrel();
                    #endif
                    //webBuilder.ConfigureKestrel(options =>
                    //{
                    //    int httpsPort = 5001;
                    //    string pfxFilePath = "/var/www/asp.net/certs/api.novastudios.tk.crt";
                    //    options.Listen(IPAddress.Any, httpsPort, listenOptions =>
                    //    {
                    //        listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
                    //        listenOptions.UseHttps(pfxFilePath);
                    //    });
                    //});
                    webBuilder.UseStartup<Startup>();
                });
    }
}
