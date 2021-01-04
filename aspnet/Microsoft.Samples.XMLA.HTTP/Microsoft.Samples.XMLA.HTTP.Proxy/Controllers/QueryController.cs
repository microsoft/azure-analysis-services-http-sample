using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
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


        [HttpGet("/api/Databases")]
        public async Task<IActionResult> GetDatabases(CancellationToken cancel)
        {
            var query = @"select [CATALOG_NAME] from $SYSTEM.DBSCHEMA_CATALOGS";
            log.LogInformation("Begin Get Request");
            return await GetQueryResult(config.DefaultDatabase, query, false, cancel); //always run at default database
        }

        [HttpGet("/api/Tables")]
        public async Task<IActionResult> GetTables(
            CancellationToken cancel) => await GetTables(config.DefaultDatabase, cancel);

        [HttpGet("/api/{database}/Tables")]
        public async Task<IActionResult> GetTables(
            [FromRoute] string database, 
            CancellationToken cancel)
        {
            var query = @"select TABLE_NAME 
            from $SYSTEM.DBSCHEMA_TABLES
            where TABLE_SCHEMA <> '$SYSTEM'";
            log.LogInformation("Begin Get Request");
            return await GetQueryResult(database, query, false, cancel);
        }

        [HttpGet("/api/Tables/{table}")]
        public async Task<IActionResult> GetTable(
            [FromRoute] string table,
            CancellationToken cancel) => await GetTable(config.DefaultDatabase, table, cancel);

        [HttpGet("/api/{database}/Tables/{table}")]
        public async Task<IActionResult> GetTable(
            [FromRoute] string database, 
            [FromRoute]string table, 
            CancellationToken cancel)
        {
            var query = $"evaluate({QuoteName(table)})";
            log.LogInformation("Begin Get Request");
            return await GetQueryResult(database ?? config.DefaultDatabase, query, false, cancel);
        }

        [HttpGet("/api/Query")]
        public async Task<IActionResult> Get(
            [FromQuery] string query,
            [FromQuery] bool? gzip,
            CancellationToken cancel) => await Get(config.DefaultDatabase, query, gzip, cancel);
            

        [HttpGet("/api/{database}/Query")]
        public async Task<IActionResult> Get(
            [FromRoute]string database,
            [FromQuery]string query, 
            [FromQuery]bool? gzip, 
            CancellationToken cancel)
        {

            log.LogInformation("Begin Get Request");
            return await GetQueryResult(database, query, gzip??false, cancel);
        }


        [HttpPost("/api/Query")]
        public async Task<IActionResult> Post(
            [FromQuery] bool? gzip,
            CancellationToken cancel) => await Post(config.DefaultDatabase, gzip, cancel);


        [HttpPost("/api/{database}/Query")]
        public async Task<IActionResult> Post(
            [FromRoute]string database, 
            [FromQuery]bool? gzip, 
            CancellationToken cancel)
        {
            var sr = new StreamReader(Request.Body);
            string query = await sr.ReadToEndAsync();

            log.LogInformation("Begin Post Request");
            return await GetQueryResult(database, query, gzip??false, cancel);

        }

        [NonAction]
        private async Task<IActionResult> GetQueryResult(string database, string query, bool gzip, CancellationToken cancel)
        {
            if (string.IsNullOrEmpty(database))
            {
                throw new ArgumentException("Database not specified in route and no default database configured");
            }

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

                var cs = constr.Split(";");
                var sb = new StringBuilder();
                foreach (var c in cs)
                {
                    if (c.Trim().StartsWith("password", StringComparison.InvariantCultureIgnoreCase))
                    {
                        sb.Append("password=********").Append(";");
                    }
                    else
                    {
                        sb.Append(c).Append(";");
                    }
                }


                log.LogError($"Failed to get ADODB connection for connection string: {msg}: {sb.ToString()}");
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

        static string QuoteName(string identifier)
        {
            var sb = new System.Text.StringBuilder(identifier.Length + 3, 1024);
            sb.Append('\'');
            foreach (var c in identifier)
            {
                if (c == '\'')
                {
                    sb.Append("''");
                }
                else
                {
                    sb.Append(c);
                }
            }
            sb.Append('\'');
            return sb.ToString();

        }


    }
}
