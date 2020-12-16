using Microsoft.AspNetCore.Http;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;

namespace Microsoft.Samples.XMLA.HTTP.Proxy
{

    public enum AuthScheme
    {
        NONE,
        BASIC,
        BEARER
    }
    public class AuthData
    {
        public AuthData(HttpRequest req)
        {
            var authHeaderValue = req.Headers["Authorization"].FirstOrDefault();
            if (authHeaderValue == null)
            {
                Scheme = AuthScheme.NONE;
                return;
            }
            
            var authHeader = AuthenticationHeaderValue.Parse(authHeaderValue);

            if (authHeader.Scheme.Equals("bearer",StringComparison.OrdinalIgnoreCase))
            {
                Scheme = AuthScheme.BEARER;
                PasswordOrToken = authHeader.Parameter;
                return;
            }
            else if (authHeader.Scheme.Equals("basic",StringComparison.OrdinalIgnoreCase))
            {
                var auth = authHeader.Parameter;
                var authString = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(auth));
                var aa = authString.Split(':');
                if (aa.Length==3)
                {
                    var user = aa[0] + ":" + aa[1];
                    var pwd = aa[2];

                    Scheme = AuthScheme.BASIC;
                    UPN = user;
                    PasswordOrToken = pwd;
                    return;
                }
                else if (aa.Length == 2)
                {
                    var user = aa[0];
                    var pwd = aa[1];

                    Scheme = AuthScheme.BASIC;
                    UPN = user;
                    PasswordOrToken = pwd;
                    return;
                }
               
            }
            this.Scheme = AuthScheme.NONE;
            return;



        }
        public AuthScheme Scheme { get; set; }

        public string UPN { get; set; }
        public string PasswordOrToken { get; set; }

        public DateTime ValidTo { get; set; }
    }
    
}
