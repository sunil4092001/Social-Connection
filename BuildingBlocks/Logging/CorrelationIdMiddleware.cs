using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace BuildingBlocks.Logging
{
    public class CorrelationIdMiddleware
    {
        private const string HeaderName = "X-Correlation-Id";

        private readonly RequestDelegate _next;

        public CorrelationIdMiddleware(RequestDelegate next)
        {
         _next = next;     
        }

        public async Task Invoke(HttpContext context)
        {
            var correlationId =
                context.Request.Headers[HeaderName]
                .FirstOrDefault()
                ?? Guid.NewGuid().ToString();

            context.Response.Headers[HeaderName] = correlationId;

            using (Serilog.Context.LogContext.PushProperty("CorrelationId",correlationId))
            {
                await _next(context);
            }
        }


    }
}
