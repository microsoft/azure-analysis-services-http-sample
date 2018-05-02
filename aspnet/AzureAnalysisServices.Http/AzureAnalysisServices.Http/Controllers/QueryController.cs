using System;
using System.Collections.Generic;
using System.Configuration;
using System.Web.Http.Tracing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System.Net.Http.Headers;
using System.Threading;

namespace Microsoft.Samples.AzureAnalysisServices.Http.Controllers
{
    public class QueryController : ApiController
    {


        static QueryController()
        {
            ServicePointManager.DefaultConnectionLimit = Int32.MaxValue;
        }


        [AcceptVerbs("POST", "GET")]
       // [Route("query")]
        public async Task<HttpResponseMessage> Run(CancellationToken cancel)
        {
            var log = Configuration.Services.GetTraceWriter();
            HttpRequestMessage req = Request;
            log.Info(req,"Query","Request Begin request.");

            //don't support duplicates of query string parameters
            var queryString = req.GetQueryNameValuePairs().ToDictionary(p => p.Key, p => p.Value);

            //var server = @"asazure://southcentralus.asazure.windows.net/dbrowneaas";


            var authData = new AuthData(req);
            if (authData.Scheme == AuthScheme.NONE)
            {
                var challengeResponse = req.CreateErrorResponse(HttpStatusCode.Unauthorized, "Unauthorized");
                challengeResponse.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue("Bearer", @"realm=""*.asazure.windows.net"""));
                if (req.RequestUri.Scheme == "https")
                {
                    challengeResponse.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue("Basic", @"realm=""*.asazure.windows.net"""));
                }
                return challengeResponse;

            }

            if (authData.Scheme == AuthScheme.BASIC && req.RequestUri.Scheme == "http")
            {
                return req.CreateErrorResponse(HttpStatusCode.BadRequest, "HTTP Basic Auth only supported with https requests.");
            }


            
            var server = ConfigurationManager.AppSettings["Server"];
            var database = ConfigurationManager.AppSettings["Database"];

            if (string.IsNullOrEmpty(server))
            {
                throw new InvalidOperationException("Required AppSettings Server is missing.");
            }
            if (string.IsNullOrEmpty(database))
            {
                throw new InvalidOperationException("Required AppSettings Database is missing.");
            }

            string constr;
            if (authData.Scheme == AuthScheme.BASIC)
            {
                constr = $"Data Source={server};User Id={authData.UPN};Password={authData.PasswordOrToken};Catalog={database};Persist Security Info=True; Impersonation Level=Impersonate";
            }
            else if (authData.Scheme == AuthScheme.BEARER)
            {
                constr = $"Data Source={server};Password={authData.PasswordOrToken};Catalog={database};Persist Security Info=True; Impersonation Level=Impersonate";
            }
            else
            {
                throw new InvalidOperationException($"unexpected state authData.Scheme={authData.Scheme}");
            }

            //get gzip setting
            bool gzip = queryString.ContainsKey("gzip") ? bool.Parse(queryString["gzip"]) : true;
            //if (req.Headers.AcceptEncoding.Any(h => h.Value == "gzip" || h.Value == "*"))
            //{
            //    gzip = true;
            //}

            string query;
            if (req.Method == HttpMethod.Get)
            {
                if (!queryString.ContainsKey("query"))
                {
                    return req.CreateErrorResponse(HttpStatusCode.BadRequest, "get request must include 1 'query' query string parameter", new ArgumentException("query"));
                }
                query = queryString["query"];
            }
            else
            {
                query = await req.Content.ReadAsStringAsync();
            }

            var con = ConnectionPool.Instance.GetConnection(constr, authData);

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
                    log.Info(Request, "Query", "Query Execution Canceled");
                });
                queryResults = cmd.Execute();
            }
            catch (Exception ex)
            {
                return req.CreateErrorResponse(HttpStatusCode.InternalServerError, ex);
            }

            var resp = (HttpResponseMessage)req.CreateResponse();
            resp.StatusCode = HttpStatusCode.OK;

            var streaming = true;
            //var indent = true;


            resp.Content = new PushStreamContent(async (responseStream, content, transportContext) =>
            {
                try
                {

                    System.IO.Stream encodingStream = responseStream;
                    if (gzip)
                    {
                        encodingStream = new System.IO.Compression.GZipStream(responseStream, System.IO.Compression.CompressionMode.Compress, false);
                    }

                    using (responseStream)
                    using (encodingStream)
                    {

                        if (streaming)
                        {
                            await ResultWriter.WriteResultsToStream(queryResults, encodingStream, cancel);
                        }
                        else
                        {
                            var ms = new MemoryStream();
                            await ResultWriter.WriteResultsToStream(queryResults, ms, cancel);
                            ms.Position = 0;
                            var buf = new byte[256];
                            ms.Read(buf, 0, buf.Length);
                            var str = System.Text.Encoding.UTF8.GetString(buf);
                            log.Info(Request, "Query", $"buffered query results starting {str}");
                            ms.Position = 0;

                            ms.CopyTo(encodingStream);
                        }

                        ConnectionPool.Instance.ReturnConnection(con);
                        await encodingStream.FlushAsync();
                        await responseStream.FlushAsync();


                    }
                }
                catch (Exception ex)
                {
                    log.Error(Request,"Query",ex);
                    con.Connection.Dispose();//do not return to pool
                    throw;
                }


            }, "application/json");
            if (gzip)
            {
                resp.Content.Headers.ContentEncoding.Add("gzip");
            }

            return resp;



        }


       
    }
}