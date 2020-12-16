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
using Microsoft.Samples.XMLA.HTTP.Proxy;

namespace Microsoft.Samples.XMLA.HTTP.Proxy.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class QueryController : ControllerBase
    {

        private readonly ILogger<QueryController> log;

        private readonly Config config;
        private ConnectionPool pool;
        private TokenHelper tokenHelper;

        public QueryController(ILogger<QueryController> logger, Config config, ConnectionPool pool, TokenHelper tokenHelper)
        {
            this.log = logger;
            this.config = config;
            this.pool = pool;
            this.tokenHelper = tokenHelper;
        }

        [HttpGet("/api/Tables")]
        public async Task<IActionResult> GetTables(CancellationToken cancel)
        {
            var query = @"select TABLE_NAME 
            from $SYSTEM.DBSCHEMA_TABLES
            where TABLE_SCHEMA <> '$SYSTEM'";
            log.LogInformation("Begin Get Request");
            return await GetQueryResult(query, false, cancel);
        }

        [HttpGet("/api/Tables/{table}")]
        public async Task<IActionResult> GetTables(string table, CancellationToken cancel)
        {
            var query = $"evaluate({table})";
            log.LogInformation("Begin Get Request");
            return await GetQueryResult(query, false, cancel);
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

        [NonAction]
        private async Task<IActionResult> GetQueryResult(string query, bool gzip, CancellationToken cancel)
        {
            

            var req = this.Request;

            //Authenticate the request
            var authData = new AuthData(req);
            if (authData.Scheme == AuthScheme.NONE  && !config.IsSSAS)
            {

                var bearerHeader = new AuthenticationHeaderValue("Bearer", @"realm=""*.asazure.windows.net""");
                //WWW-Authenticate: Basic realm="Access to staging site"

                if (req.IsHttps)
                {
                    var basicHeader = new AuthenticationHeaderValue("Basic", @"realm=""*.asazure.windows.net""");
                    return new UnauthorizedResultWithAuthenticationheader(bearerHeader, basicHeader);
                }
                log.LogInformation("Returning 401 Authrorization Challenge");
                return new UnauthorizedResultWithAuthenticationheader(bearerHeader);
            }

            if (authData.Scheme == AuthScheme.BASIC && !req.IsHttps)
            {
                //If behind a gateway with SSL Terminiation this might be OK.
                log.LogInformation("Rejecting HTTP Basic Auth over non-encryted connection.");
                return BadRequest("HTTP Basic Auth only supported with https requests.");
            }

            var server = config.Server;
            var database = config.Database;
            var tenantId = config.TenantId;

            string constr = await tokenHelper.GetConnectionString(authData, server, database, tenantId);

            ConnectionPoolEntry con;
            try
            {
                con = pool.GetConnection(constr, authData);
            }
            catch (Exception ex)
            {
                if (ex is TargetInvocationException tex)
                {
                    ex = tex.InnerException;
                }
                var msg = ex.Message;

                log.LogError($"Failed to get ADODB connection for connection string: {msg}: {constr}");
                return this.Problem("Failed to get ADODB connection for connection string.  See server log for details.");
                
            }

            var cmd = con.Connection.CreateCommand();
            cmd.CommandText = query;

            object queryResults;

            try
            {
                cmd.CommandTimeout = 2 * 60;
                var reg = cancel.Register(() =>
                {
                    cmd.Cancel();
                    con.Connection.Dispose();
                    log.LogInformation("Query Execution Canceled by Controller's Cancellation Token");
                });
                queryResults = cmd.Execute();
                reg.Unregister();
            }
            catch (Exception ex)
            {
                return Problem(ex.Message);
            }

            
            if (queryResults is AdomdDataReader rdr)
            {
                return new QueryResult(rdr, gzip, false, con, pool, log);
            }
            else 
            {
                return BadRequest($"Query execution returned unsupported result type {queryResults.GetType().Name}.  Must be AdomdDataReader.");
            }
            

            
        }
    }
}
