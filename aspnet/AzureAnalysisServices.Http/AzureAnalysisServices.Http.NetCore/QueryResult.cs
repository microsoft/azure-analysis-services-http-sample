using Microsoft.AnalysisServices.AdomdClient;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Microsoft.Samples.AzureAnalysisServices.Http.NetCore
{
    class QueryResult : ActionResult
    {
        private AdomdDataReader queryResults;
        private ILogger log;
        private bool gzip;
        private bool bufferResults;
        private ConnectionPoolEntry con;

        public QueryResult(AdomdDataReader queryResults, bool gzip, bool bufferResults, ConnectionPoolEntry con, ILogger log)
        {
            this.queryResults = queryResults;
            this.log = log;
            this.gzip = gzip;
            this.bufferResults = bufferResults;
            this.con = con;
        }

        public override async Task ExecuteResultAsync(ActionContext context)
        {

            context.HttpContext.Response.ContentType = "application/json";
            context.HttpContext.Response.StatusCode = (int)HttpStatusCode.OK;
            if (gzip)
            {
                context.HttpContext.Response.Headers.Add("Content-Encoding", "gzip");
            }
            var streaming = true;

            await context.HttpContext.Response.StartAsync();

            var responseStream = context.HttpContext.Response.Body;
            System.IO.Stream encodingStream = responseStream;
            if (gzip)
            {
                encodingStream = new System.IO.Compression.GZipStream(responseStream, System.IO.Compression.CompressionMode.Compress, false);
            }

            try
            {
                if (streaming)
                {
                    await WriteResultsToStream(queryResults, encodingStream, context.HttpContext.RequestAborted, log);
                }
                else
                {
                    var ms = new MemoryStream();
                    await WriteResultsToStream(queryResults, ms, context.HttpContext.RequestAborted, log);
                    ms.Position = 0;
                    var buf = new byte[256];
                    ms.Read(buf, 0, buf.Length);
                    var str = System.Text.Encoding.UTF8.GetString(buf);
                    log.LogInformation($"buffered query results starting {str}");
                    ms.Position = 0;

                    ms.CopyTo(encodingStream);
                }

                ConnectionPool.Instance.ReturnConnection(con);
                await encodingStream.FlushAsync();
                await responseStream.FlushAsync();


            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error writing results");
                con.Connection.Dispose(); //do not return to pool
                throw;  //too late to send error to client  
            }
            finally
            {
                encodingStream.Dispose();
            }

        }

        static System.Text.UTF8Encoding encoding = new(false);

        static async Task WriteResultsToStream(AdomdDataReader results, Stream stream, CancellationToken cancel, Extensions.Logging.ILogger log)
        {
            
            if (results == null)
            {
                log.LogInformation("Null results");
                return;
            }


            using var rdr = results;
            using var tw = new StreamWriter(stream, encoding, 1024 * 4, true);
            using var w = new Newtonsoft.Json.JsonTextWriter(tw);

            int rows = 0;
            await w.WriteStartObjectAsync(cancel);
            var rn = "rows";

            await w.WritePropertyNameAsync(rn);
            await w.WriteStartArrayAsync(cancel);
                    
            while (rdr.Read())
            {
                rows++;
                await w.WriteStartObjectAsync(cancel);
                for (int i = 0; i < rdr.FieldCount; i++)
                {
                    string name = rdr.GetName(i);
                    object value = rdr.GetValue(i);

                    await w.WritePropertyNameAsync(name, cancel);
                    await w.WriteValueAsync(value, cancel);
                }
                await w.WriteEndObjectAsync(cancel);
            }
            log.LogInformation($"Wrote {rows} AdomdDataReader rows to output stream.");

            await w.WriteEndArrayAsync(cancel);
            await w.WriteEndObjectAsync(cancel);

            await w.FlushAsync();
            await tw.FlushAsync();
            await stream.FlushAsync();


        }
    }
}
