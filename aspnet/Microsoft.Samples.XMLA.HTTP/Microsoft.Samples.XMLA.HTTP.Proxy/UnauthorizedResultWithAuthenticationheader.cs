using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Samples.XMLA.HTTP.Proxy
{
    public class UnauthorizedResultWithAuthenticationheader : ActionResult
    {
        private AuthenticationHeaderValue[] authHeaderValues;

        public UnauthorizedResultWithAuthenticationheader(params AuthenticationHeaderValue[] authHeaderValues) 
        {
            this.authHeaderValues = authHeaderValues;
        }


        public override async Task ExecuteResultAsync(ActionContext context)
        {
            var sb = new StringBuilder();
            foreach (var v in authHeaderValues)
            {
                sb.Append(v.ToString()).Append(", ");
            }
            sb.Length = sb.Length - 2;
            var hv = sb.ToString();

            context.HttpContext.Response.Headers.Add("WWW-Authenticate", hv);
            context.HttpContext.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            await context.HttpContext.Response.CompleteAsync();

        }

    }
}
