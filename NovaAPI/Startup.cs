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
using MySql.Data.MySqlClient;
using NovaAPI.Util;
using NovaAPI.Attri;

namespace NovaAPI
{
    public class Startup
    {
        public static string API_Domain { get; set; }
        public static string Interface_Domain { get; set; }
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
            
            // Load MySql config
            IConfigurationSection config = Configuration.GetSection("SQLServerConfig");
            MySqlServer.AutoConfig = bool.Parse(config.GetSection("AutoConfig").Value);
            MySqlServer.Server = config.GetSection("Server").Value;
            MySqlServer.Port = config.GetSection("Port").Value;
            MySqlServer.User = config.GetSection("User").Value;
            MySqlServer.Password = config.GetSection("Password").Value;
            MySqlServer.UserDatabaseName = config.GetSection("UserDatabaseName").Value;
            MySqlServer.ChannelsDatabaseName = config.GetSection("ChannelDatabaseName").Value;
            MySqlServer.MasterDatabaseName = config.GetSection("MasterDatabaseName").Value;

            // Load email configuration
            IConfigurationSection mailServerConfig = Configuration.GetSection("MailServerConfig");
            IConfigurationSection mailSetup = mailServerConfig.GetSection("MailSetup");
            EmailConfig.VerifyEmail = bool.Parse(mailServerConfig.GetSection("VerifyEmail").Value);
            EmailConfig.PasswordReset = bool.Parse(mailServerConfig.GetSection("PasswordReset").Value);
            EmailConfig.SMTPPort = int.Parse(mailSetup.GetSection("SMTPPort").Value);
            EmailConfig.SMTPHost = mailSetup.GetSection("SMTPHost").Value;
            EmailConfig.FromAddress = mailSetup.GetSection("FromAddress").Value;
            EmailConfig.Username = mailSetup.GetSection("Username").Value;
            EmailConfig.Password = mailSetup.GetSection("Password").Value;
            
            
            if (MySqlServer.AutoConfig)
            {
                // Setup databases
                using MySqlConnection conn = new(MySqlServer.CreateSQLString());
                conn.Open();

                // Create User Database
                new MySqlCommand($"CREATE DATABASE IF NOT EXISTS `{MySqlServer.UserDatabaseName}`", conn)
                    .ExecuteNonQuery();
                // Create Channels Database
                new MySqlCommand($"CREATE DATABASE IF NOT EXISTS `{MySqlServer.ChannelsDatabaseName}`", conn)
                    .ExecuteNonQuery();
                // Create Master Database
                new MySqlCommand($"CREATE DATABASE IF NOT EXISTS `{MySqlServer.MasterDatabaseName}`", conn)
                    .ExecuteNonQuery();

                conn.Close();

                // Create Master Database Tables
                using MySqlConnection masterCon =
                    new MySqlConnection(MySqlServer.CreateSQLString(MySqlServer.MasterDatabaseName));
                masterCon.Open();

                // Create Users Table
                new MySqlCommand(MySqlServer.UserTableString, masterCon).ExecuteNonQuery();
                // Create Channels Table
                new MySqlCommand(MySqlServer.ChannelTableString, masterCon).ExecuteNonQuery();
                // Create ChannelMedia Table
                new MySqlCommand(MySqlServer.ChannelMediaTableString, masterCon).ExecuteNonQuery();
                // Create GetDiscriminator Function
                MySqlScript discrim = new MySqlScript(masterCon);
                discrim.Query = MySqlServer.DiscrimnatorGen;
                discrim.Delimiter = "$$";
                discrim.Execute();

                masterCon.Close();
            }
            
            IConfigurationSection genConfig = Configuration.GetSection("GeneralServerConfig");
            
            // Setup storage
            StorageUtil.InitStorage(genConfig.GetSection("APIDataDirectory").Value, Configuration);
            API_Domain = genConfig.GetSection("API_Domain").Value;
            Interface_Domain = genConfig.GetSection("Interface_Domain").Value;

            services.AddCors(options =>
            {
                options.AddPolicy(name: "Origins", builder =>
                {
                    builder.WithOrigins("http://localhost:3000", "https://live.orbit.novastudios.uk", "https://orbit.novastudios.uk");
                    builder.WithMethods("DELETE", "GET", "POST", "PATCH", "PUT", "UPGRADE");
                    builder.WithHeaders("authorization", "content-type");
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
                    Title = "Nova Stable API", 
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
