using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Samples.AzureAnalysisServices.Http
{
    class TokenHelper
    {
        internal static JwtSecurityToken ReadToken(string bearerToken)
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
    }
}
