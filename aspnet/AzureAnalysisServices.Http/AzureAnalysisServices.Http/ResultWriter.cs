using Microsoft.AnalysisServices.AdomdClient;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Samples.AzureAnalysisServices.Http
{
    class ResultWriter
    {
        public static async Task WriteResultsToStream(object results, Stream stream, CancellationToken cancel)
        {
            
            if (results == null)
            {
                return;
            }

            if (results is AdomdDataReader rdr)
            {
                var encoding = new System.Text.UTF8Encoding(false);
                using (var tw = new StreamWriter(stream,encoding,1024*4,true))
                using (var w = new Newtonsoft.Json.JsonTextWriter(tw))
                {
                    
                    await w.WriteStartObjectAsync(cancel);
                    var rn = "rows";

                    await w.WritePropertyNameAsync(rn);
                    await w.WriteStartArrayAsync(cancel);
                    
                    while (rdr.Read())
                    {
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

                    await w.WriteEndArrayAsync(cancel);
                    await w.WriteEndObjectAsync(cancel);

                    await w.FlushAsync();
                    await tw.FlushAsync();
                    await stream.FlushAsync();
                }
            }
            else if (results is CellSet  cs)
            {
                throw new NotSupportedException("CellSet results");

            }
            else
            {
                throw new InvalidOperationException("Unexpected result type");
            }

        }
    }
}
