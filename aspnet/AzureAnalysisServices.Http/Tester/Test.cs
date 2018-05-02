using System.Net;

namespace Tester
{
    public class Test
    {
        public string Host { get; set; }
        public int HttpPort { get; set; } = 80;
        public int HttpsPort { get; set; } = 443;
        public string Path { get; set; } = "/api/Query";
        public string Query { get; set; }
        public bool GZip { get; set; } = true;
        public string HttpMethod { get; set; } = "get";
        public string Body { get; set; } = "";
        public string AuthScheme { get; set; } = "bearer";
        public bool UseHttps { get; set; } = true;

        public string RequestUri => $"{(UseHttps ? "https://" : "http://")}{Host}:{(UseHttps ? HttpsPort : HttpPort)}/{Path}";
        
        public HttpStatusCode ExpectedResponse { get; set; } =  HttpStatusCode.OK;

        public Test Clone()
        {
            return new Test() {
                Host = this.Host,
                HttpPort = this.HttpPort,
                HttpsPort = this.HttpsPort,
                Path = this.Path,
                HttpMethod = this.HttpMethod,
                Body = this.Body,
                AuthScheme = this.AuthScheme,
                ExpectedResponse = this.ExpectedResponse,
                GZip = this.GZip,
                Query = this.Query,
                UseHttps = this.UseHttps
            };


        }
    }
}
