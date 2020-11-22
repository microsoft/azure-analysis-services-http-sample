using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AnalysisServices.AdomdClient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Samples.AzureAnalysisServices.Http.NetCore;

namespace AzureAnalysisServices.Http.NetCore.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class QueryController : ControllerBase
    {

        private readonly ILogger<QueryController> log;

        private readonly IConfiguration config;

        public QueryController(ILogger<QueryController> logger, IConfiguration config)
        {
            this.log = logger;
            this.config = config;
        }

        [HttpGet]
        public async Task<IActionResult> Get(
            [FromQuery]
            string query, 
            
            [FromQuery]bool? gzip, 
            
            CancellationToken cancel)
        {

            log.LogInformation("Begin Get Request");
            return await GetQueryResult(query, gzip??false, cancel);
        }

        
        [HttpPost]
        public async Task<IActionResult> Post( [FromQuery]bool? gzip, CancellationToken cancel)
        {
            var sr = new StreamReader(Request.Body);
            string query = await sr.ReadToEndAsync();

            log.LogInformation("Begin Post Request");
            return await GetQueryResult(query, gzip??false, cancel);
        }


        private async Task<IActionResult> GetQueryResult(string query, bool gzip, CancellationToken cancel)
        {
            

            var req = this.Request;

            //Authenticate the request
            var authData = new AuthData(req);
            if (authData.Scheme == AuthScheme.NONE)
            {

                var bearerHeader = new AuthenticationHeaderValue("Bearer", @"realm=""*.asazure.windows.net""");
                //WWW-Authenticate: Basic realm="Access to staging site"

                if (req.IsHttps)
                {
                    var basicHeader = new AuthenticationHeaderValue("Basic", @"realm=""*.asazure.windows.net""");
                    return new UnauthorizedResultWithAuthenticationheader(bearerHeader, basicHeader);
                }
                return new UnauthorizedResultWithAuthenticationheader(bearerHeader);
            }

            if (authData.Scheme == AuthScheme.BASIC && !req.IsHttps)
            {
                return BadRequest("HTTP Basic Auth only supported with https requests.");
            }

            var server = config.GetValue<string>("Server");
            var database = config.GetValue<string>("Database");
            var tenantId = config.GetValue<string>("TenantID");

            if (string.IsNullOrEmpty(server))
            {
                throw new InvalidOperationException("Required Config value Server is missing.");
            }
            if (string.IsNullOrEmpty(database))
            {
                throw new InvalidOperationException("Required AppSettings Database is missing.");
            }


            string constr = await ConnectionPool.GetConnectionString(authData, server, database, tenantId);

            ConnectionPoolEntry con;
            try
            {
                con = ConnectionPool.Instance.GetConnection(constr, authData);
            }
            catch (Exception ex)
            {
                if (ex is TargetInvocationException tex)
                {
                    ex = tex.InnerException;
                }
                var msg = ex.Message;

                log.LogError($"Failed to get ADODB connection for connection string: {msg}");
                return this.Problem("Failed to get ADODB connection for connection string.  See server log for details.");
                
            }


            var cmd = con.Connection.CreateCommand();
            cmd.CommandText = query;

            object queryResults;

            try
            {
                cmd.CommandTimeout = 2 * 60;
                cancel.Register(() =>
                {
                    cmd.Cancel();
                    con.Connection.Dispose();
                    log.LogInformation("Query Execution Canceled");
                });
                queryResults = cmd.Execute();
            }
            catch (Exception ex)
            {
                return Problem(ex.Message);
            }

            
            if (queryResults is AdomdDataReader rdr)
            {
                return new QueryResult(rdr, gzip, false, con, log);
            }
            else 
            {
                return BadRequest($"Query execution returned unsupported result type {queryResults.GetType().Name}.  Must be AdomdDataReader.");
            }
            

            
        }
    }
}
