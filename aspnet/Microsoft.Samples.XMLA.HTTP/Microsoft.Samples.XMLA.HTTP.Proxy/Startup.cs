using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;

namespace Microsoft.Samples.XMLA.HTTP.Proxy
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {

            services.AddControllers();

            services.AddSingleton(typeof(Config));
            services.AddSingleton(typeof(TokenHelper));
            services.AddSingleton(typeof(ConnectionPool));
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "Microsoft.Samples.XMLA.HTTP.Proxy",
                    Version = "v1",
                    Description = "XML/A HTTP Sample for Azure Analysis Services and Power BI Premium",
                    License = new OpenApiLicense
                    {
                        Name = "MIT",
                        Url = new Uri("https://github.com/microsoft/azure-analysis-services-http-sample/blob/master/LICENSE")
                    }

                });

                c.AddSecurityDefinition("Basic",
                    new OpenApiSecurityScheme()
                    {
                        Description = "XMLA Endpoint Credentials in HTTP Basic Auth format.  For a service Principal use app:[ClientId]@[TenantId], and pass a ClientSecret as the password.",
                        Scheme = "BASIC",
                        Type = SecuritySchemeType.Http
                    });

                OpenApiSecurityScheme securityDefinition = new OpenApiSecurityScheme()
                {
                    Name = "Bearer",
                    BearerFormat = "JWT",
                    Scheme = "bearer",
                    Description = "Bearer Auth with a token for AAS or Power BI.  The token should be for 'https://analysis.windows.net/powerbi/api' or 'https://*.asazure.windows.net'",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.Http,
                };
                c.AddSecurityDefinition("Bearer", securityDefinition);


                c.AddSecurityRequirement(new OpenApiSecurityRequirement()
                {
                    {
                        new OpenApiSecurityScheme()
                            {
                                Reference = new OpenApiReference()
                                {
                                    Id = "Basic", //The name of the previously defined security scheme.
                                    Type = ReferenceType.SecurityScheme
                                }
                            },new List<string>()
                    }

                });

                c.AddSecurityRequirement(new OpenApiSecurityRequirement()
                {
                    {
                        new OpenApiSecurityScheme()
                            {
                                Reference = new OpenApiReference()
                                {
                                    Id = "Bearer", //The name of the previously defined security scheme.
                                    Type = ReferenceType.SecurityScheme
                                }
                            },new List<string>()
                    }

                });


            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Microsoft.Samples.XMLA.HTTP.Proxy v1"));
            }

            //app.UseHttpsRedirection();

            app.UseRouting();

            //app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
