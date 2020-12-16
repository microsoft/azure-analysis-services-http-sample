using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Samples.XMLA.HTTP.Proxy
{
    public class TokenHelper
    {
        string resourceId;
        Config config;
        public TokenHelper(Config config)
        {
            this.config = config;
            resourceId = config.ResourceId;
        }
        internal  JwtSecurityToken ReadToken(string bearerToken)
        {
            var handler = new JwtSecurityTokenHandler();
            //if handler.CanReadToken(bearerToken);
            var token = handler.ReadJwtToken(bearerToken);
            //var upn = token.Claims.Single(c => c.Type == "upn").Value;
            //validTo = token.ValidTo.ToLocalTime();
            //var validDuration = token.ValidTo.Subtract(DateTime.UtcNow);
            //log.Info($"bearer token recievied for {upn} valid for {(int)validDuration.TotalMinutes}min {((int)validDuration.TotalSeconds) % 60}sec");
            return token;
        }

        internal  async Task<string> GetBearerTokenAsync(string clientId, string clientSecret, string tenantId)
        {
            var authority = $"https://login.microsoftonline.com/{tenantId}";
            var ctx = new AuthenticationContext(authority);
            var token = await ctx.AcquireTokenAsync(resourceId, new ClientCredential(clientId, clientSecret));

            return token.AccessToken;
        }

        public async Task<string> GetConnectionString(AuthData authData, string server, string database, string tenantId)
        {
            string constr;
            if (authData.Scheme == AuthScheme.NONE ) //int
            {
                if (!config.IsSSAS)
                {
                    throw new InvalidOperationException("Anonymous Access is intended for testing with SSAS only.");
                }
                constr = $"Data Source={server};Catalog={database};";//Persist Security Info=True; Impersonation Level=Impersonate";

            }
            else if (authData.Scheme == AuthScheme.BASIC)
            {
                var parts = authData.UPN.Split('@');

                if (false && authData.UPN.StartsWith("app:"))  //app:[ClientId]@[TenantID]  Client provided both ClientId and ClientSecret
                {
                    var split = authData.UPN.Split('@');
                    var clientId = split[0][4..];
                    var tenantIdFromAuthHeader = split[1];

                    if (!Guid.TryParse(clientId, out _ ))
                    {
                        throw new InvalidOperationException("HTTP Basic Auth invalid ClientID in UPN. Should be app:[ClientId]@[TenantID]");
                    }
                    if (!Guid.TryParse(tenantIdFromAuthHeader, out _))
                    {
                        throw new InvalidOperationException("HTTP Basic Auth invalid TenantId in UPN. Should be app:[ClientId]@[TenantID]");
                    }

                    var token = await GetBearerTokenAsync(clientId, authData.PasswordOrToken, tenantIdFromAuthHeader);
                    constr = $"Data Source={server};Password={token};Catalog={database};";//Persist Security Info=True; Impersonation Level=Impersonate";


                }
                else if (Guid.TryParse(authData.UPN, out _)) //assume it's a clientId and password is a ClientSecret, and add the tenantId from web.config to fetch a token
                {
                    var token = await GetBearerTokenAsync(authData.UPN, authData.PasswordOrToken, tenantId);
                    constr = $"Data Source={server};Password={token};Catalog={database};";//Persist Security Info=True; Impersonation Level=Impersonate";

                }
                else
                {
                    //let adodb.net try to auth with th UPN/Password
                    constr = $"Data Source={server};User Id={authData.UPN};Password={authData.PasswordOrToken};Catalog={database};";// Persist Security Info=True; Impersonation Level=Impersonate";

                }

            }
            else if (authData.Scheme == AuthScheme.BEARER)
            {
                constr = $"Data Source={server};Password={authData.PasswordOrToken};Catalog={database};Persist Security Info=True; Impersonation Level=Impersonate";
            }
            else
            {
                throw new InvalidOperationException($"unexpected state authData.Scheme={authData.Scheme}");
            }

            return constr;
        }

    }
}
