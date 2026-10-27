using Microsoft.AspNetCore.Builder;
using Serilog;

namespace BuildingBlocks.Logging;

public static class SerilogExtensions
{
    /// <summary>Structured logs to the console, and to Seq when Seq:ServerUrl is configured.</summary>
    public static WebApplicationBuilder AddSerilogLogging(this WebApplicationBuilder builder, string serviceName)
    {
        var seqUrl = builder.Configuration["Seq:ServerUrl"];

        builder.Services.AddSerilog((services, loggerConfiguration) =>
        {
            loggerConfiguration
                .ReadFrom.Configuration(builder.Configuration)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("ServiceName", serviceName)
                .Enrich.WithMachineName()
                .Enrich.WithEnvironmentName()
                .WriteTo.Console();

            if (!string.IsNullOrWhiteSpace(seqUrl))
            {
                loggerConfiguration.WriteTo.Seq(seqUrl);
            }
        });

        return builder;
    }
}
