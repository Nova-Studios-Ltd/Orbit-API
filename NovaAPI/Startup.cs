using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NovaAPI.Controllers;
using Microsoft.Extensions.Configuration;
using System.Net.WebSockets;
using System.Threading;
using System.Text;
using AspNetCoreRateLimit;
using NovaAPI.Util;
using NovaAPI.Attri;

namespace NovaAPI
{
    public class Startup
    {
        public Startup(IWebHostEnvironment env)
        {
            ConfigurationBuilder builder = new();
            builder.SetBasePath(env.ContentRootPath).AddJsonFile("appsettings.json", false, true).AddJsonFile($"appsettings.{env.EnvironmentName}.json", true).AddEnvironmentVariables();
            Configuration = builder.Build();
        }
        public IConfigurationRoot Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            // Configure rate limits
            services.AddMemoryCache();
            services.Configure<IpRateLimitOptions>(Configuration.GetSection("IpRateLimiting"));
            //services.Configure<IpRateLimitPolicies>(Configuration.GetSection(""));
            services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
            services.AddInMemoryRateLimiting();
            
            services.AddCors(options =>
            {
                options.AddPolicy(name: "Origins", builder =>
                {
                    builder.WithOrigins("http://localhost:3000", "https://live.orbit.novastudios.tk", "https://orbit.novastudios.tk");
                    builder.WithMethods("DELETE", "GET", "POST", "PATCH", "PUT", "UPGRADE");
                });
            });
            services.AddControllers();
            services.Add(new ServiceDescriptor(typeof(NovaChatDatabaseContext), new NovaChatDatabaseContext(Configuration)));
            services.AddTransient<EventManager>(sp =>
            {
                return new EventManager((NovaChatDatabaseContext)sp.GetService(typeof(NovaChatDatabaseContext)));
            });

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo 
                { 
                    Title = "Nova API", 
                    Version = "v1"
                });

                c.AddSecurityDefinition("basicAuth", new OpenApiSecurityScheme()
                {
                    Description = "Standard Authorization header using the Bearer scheme. Example: \"{token}\"",
                    In = ParameterLocation.Header,
                    Name = "Authorization",
                    Type = SecuritySchemeType.ApiKey
                });
                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference {
                                Type = ReferenceType.SecurityScheme,
                                Id = "basicAuth" }
                        }, new List<string>() 
                    }
                });
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            //if (env.IsDevelopment())
            //{
            //    app.UseDeveloperExceptionPage();
            //    app.UseSwagger();
            //    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "ExampleAPI v1"));
            //}

            app.UseIpRateLimiting();
            
            app.UseSwagger();
            app.UseSwaggerUI((c) => {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "NovaAPI v1");
            });

            //app.UseHttpsRedirection();
            StorageUtil.InitStorage("/var/www/asp.net/", Configuration);

            app.UseRouting();

            WebSocketOptions wsOptions = new() { KeepAliveInterval = TimeSpan.FromSeconds(120) };
            app.UseWebSockets(wsOptions);

            app.UseAuthorization();
            app.UseAuthentication();
            app.UseCors("Origins");
            
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
