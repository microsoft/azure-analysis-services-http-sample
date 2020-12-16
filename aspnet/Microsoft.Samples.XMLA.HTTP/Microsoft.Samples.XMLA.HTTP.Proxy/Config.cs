using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Samples.XMLA.HTTP.Proxy
{
    public class Config
    {
        public Config(IConfiguration configSrc, ILogger<Config> logger)
        {
            this.Server = configSrc.GetValue<string>("Server");
            this.Database = configSrc.GetValue<string>("Database");
            this.TenantId = configSrc.GetValue<string>("TenantId");

            if (string.IsNullOrEmpty(Server))
            {
                throw new InvalidOperationException("Required Config value Server is missing.");
            }
            if (string.IsNullOrEmpty(Database))
            {
                throw new InvalidOperationException("Required AppSettings Database is missing.");
            }
            if (string.IsNullOrEmpty(TenantId))
            {
                throw new InvalidOperationException("Required AppSettings TenantId is missing.");
            }

            if (Uri.TryCreate(Server, UriKind.Absolute, out Uri serverUri))
            {

                if (serverUri.Scheme == "asazure")
                {
                    ResourceId = "https://analysis.windows.net/powerbi/api";
                }
                else if (serverUri.Scheme == "powerbi")
                {
                    ResourceId = "https://*.asazure.windows.net";
                }
                else
                {
                    throw new InvalidOperationException($"Unexpected Server URI Scheme {serverUri.Scheme}");
                }
            }
            else
            {
                IsSSAS = true;
                
            }
            logger.LogInformation($"Using ResourceId {ResourceId} for target endpoint {Server}");
        }

        public bool IsSSAS { get; }
        public string Server { get; }
        public string Database { get; }
        public string TenantId { get; }
        public string ResourceId { get; }

        
    }
}
