using System;
using System.Net.Http;

namespace Microsoft.Samples.AzureAnalysisServices.Http
{

    public enum AuthScheme
    {
        NONE,
        BASIC,
        BEARER
    }
    internal class AuthData
    {
        public AuthData(HttpRequestMessage req)
        {
            if (req.Headers.Authorization == null)
            {
                Scheme = AuthScheme.NONE;
                return;
            }
            else if (req.Headers.Authorization.Scheme == "bearer")
            {
                Scheme = AuthScheme.BEARER;
                PasswordOrToken = req.Headers.Authorization.Parameter;
                return;
            }
            else if (req.Headers.Authorization.Scheme == "basic")
            {
                var auth = req.Headers.Authorization.Parameter;
                var authString = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(auth));
                var aa = authString.Split(':');
                var user = aa[0];
                var pwd = aa[1];

                Scheme = AuthScheme.BASIC;
                UPN = user;
                PasswordOrToken = pwd;
                return;
               
                //var redirectUri = new Uri("urn:ietf:wg:oauth:2.0:oob");
                //var resourceId = "https://*.asazure.windows.net";
                //var authority = "https://login.windows.net/common";
                //var clientId = "cf710c6e-dfcc-4fa8-a093-d47294e44c66";

                //var ctx = new AuthenticationContext(authority);
                //var token = await ctx.AcquireTokenAsync(resourceId, clientId, new UserPasswordCredential(user, pwd));

                //return token.AccessToken;
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
