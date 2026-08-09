using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Serilog;



namespace BuildingBlocks.Logging
{
    public static class SerilogExtensions
    {
        public static WebApplicationBuilder AddSerilogLogging( this WebApplicationBuilder builder, string serviceName)
        {
            

            builder.Services.AddSerilog((services, loggerConfiguration) =>
            {
                loggerConfiguration
                    .ReadFrom.Configuration(builder.Configuration)
                    .Enrich.FromLogContext()
                    .Enrich.WithProperty("ServiceName", serviceName)
                    .Enrich.WithMachineName()
                    .Enrich.WithEnvironmentName()
                    .WriteTo.Console()
                    .WriteTo.Seq(
                        builder.Configuration["Seq:ServerUrl"]!);
            });

            return builder;
        }
    }
}
