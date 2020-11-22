using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Tester
{


    class Program
    {
        

        static void Main(string[] args)
        {

            var host = ConfigurationManager.AppSettings["Host"];
            var httpPort = int.Parse(ConfigurationManager.AppSettings["HttpPort"]);
            var httpsPort = int.Parse(ConfigurationManager.AppSettings["HttpsPort"]);
            string clientId = ConfigurationManager.AppSettings["ClientID"];
            string clientSecret = ConfigurationManager.AppSettings["ClientSecret"];
            string tenantId = ConfigurationManager.AppSettings["TenantID"];
            string query = ConfigurationManager.AppSettings["Query"];


            var tests = new List<Test>() {
                new Test() { Host = host, HttpPort = httpPort, HttpsPort = httpsPort, Query = query, GZip = true },
                new Test() { Host = host, HttpPort = httpPort, HttpsPort = httpsPort, Query = query, GZip = false } };

            var basicHttpsTest = tests.First().Clone();
            basicHttpsTest.AuthScheme = "basic";
            basicHttpsTest.UseHttps = true;
            tests.Add(basicHttpsTest);

            var basicHttpTest = tests.First().Clone();
            basicHttpTest.AuthScheme = "basic";
            basicHttpTest.UseHttps = false;
            basicHttpTest.ExpectedResponse = HttpStatusCode.BadRequest;
            tests.Add(basicHttpTest);

            var post = tests.First().Clone();
            post.HttpMethod = "post";
            tests.Add(post);

            var noneTest = tests.First().Clone();
            noneTest.AuthScheme = "";
            noneTest.ExpectedResponse =  HttpStatusCode.Unauthorized;
            tests.Add(noneTest);

            string token = GetBearerTokenAsync(clientId,clientSecret, tenantId).Result;

            while (true)
            {
                RunTests(tests, token, clientId, clientSecret);

                Console.WriteLine("Hit 'q' to exit or any other key to rerun tests.");
                var k = Console.ReadKey();
                if (k.KeyChar == 'q')
                {
                    break;
                }
            }
        }

        private static void RunTests(List<Test> tests,  string token, string userName, string password)
        {
            
            foreach (var test in tests)
            {

                var requestUri = test.RequestUri;

                var qs = new List<Tuple<string, string>>();
                if (test.HttpMethod == "get")
                {
                    qs.Add(new Tuple<string, string>("query", test.Query));
                }
                qs.Add(new Tuple<string, string>("gzip", test.GZip.ToString()));

                for (int i = 0; i < qs.Count; i++)
                {
                    var q = qs[i];
                    string prefix = i > 0 ? "&" : "?";
                    requestUri += $"{prefix}{q.Item1}={q.Item2}";
                }
                var req = WebRequest.CreateHttp(requestUri);
                req.Method = test.HttpMethod;

                Console.WriteLine($"Running Test {test.HttpMethod}: {requestUri} with auth {test.AuthScheme} via {test.HttpMethod} ");

                if (test.AuthScheme == "bearer")
                {
                    req.Headers.Add("Authorization", $"bearer {token}");
                }
                else if (test.AuthScheme == "basic")
                {
                    //System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(auth));
                    var cred = $"{userName}:{password}";
                    var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes(cred));

                    req.Headers.Add("Authorization", $"basic {auth}");
                }

                Console.WriteLine($"  HTTP Request Headers");
                foreach (var h in req.Headers.AllKeys)
                {
                    Console.WriteLine($"  {h}: {req.Headers[h].Substring(0,50)}");
                }
        

                if (test.HttpMethod == "post")
                {
                    using (var sr = new StreamWriter(req.GetRequestStream()))
                    {
                        sr.Write(test.Query);
                    }
                       
                }
      
                HttpWebResponse resp;
                try
                {
                    resp = (HttpWebResponse)req.GetResponse();
                }
                catch (WebException ex)
                {
                    Console.WriteLine(ex.Message);
                    resp = (HttpWebResponse)ex.Response;
                    if (resp == null)
                        throw ex;
                }

                
                Console.WriteLine($"Response {(int)resp.StatusCode}({resp.StatusDescription})");
                for (int i = 0; i < resp.Headers.Count; ++i)
                {
                    Console.WriteLine("{0}: {1}", resp.Headers.Keys[i], resp.Headers[i]);
                }

                Console.WriteLine();

                var ms = new MemoryStream();
                if (resp.ContentEncoding == "gzip")
                {
                    using (var gs = new GZipStream(resp.GetResponseStream(), CompressionMode.Decompress))
                    {
                        gs.CopyTo(ms);
                    }
                }
                else
                {
                    resp.GetResponseStream().CopyTo(ms);
                }
                
                ms.Position = 0;
                var bytes = ms.ToArray();
                Console.WriteLine($"Body Length {ms.Length}: Starts [{Encoding.UTF8.GetString(bytes,0,Math.Min(1000,bytes.Length))} . . .]");

                if (resp.StatusCode == test.ExpectedResponse)
                {
                    var color = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Test passed.");
                    Console.ForegroundColor = color;
                }
                else
                {
                    var color = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Test Failed. Expected HTTP Response code:{test.ExpectedResponse} got:{resp.StatusCode}");
                    Console.ForegroundColor = color;
                }

                //if (resp.ContentEncoding != "gzip")
                //{
                //    var body = Encoding.UTF8.GetString(ms.ToArray());
                //    Console.WriteLine(body);
                //}

                Console.WriteLine();



            }
        }


        static async Task<string> GetBearerTokenAsync(string clientId, string clientSecret, string tenantId)
        {
            var redirectUri = new Uri("urn:ietf:wg:oauth:2.0:oob");
            var resourceId = "https://*.asazure.windows.net";
            var authority = $"https://login.microsoftonline.com/{tenantId}";
            // var clientId = "cf710c6e-dfcc-4fa8-a093-d47294e44c66";



            var ctx = new AuthenticationContext(authority);
            var token = await ctx.AcquireTokenAsync(resourceId, new ClientCredential(clientId,clientSecret));

            return token.AccessToken;
        }
        private static void WaitForStartup(string url)
        {
            Thread.Sleep(2000);
            while (true)
            {
                try
                {
                    var req = WebRequest.CreateHttp(url);
                    var resp = req.GetResponse();
                    resp.Close();
                    break;

                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    Thread.Sleep(2000);
                }


            }
        }
    }
}
