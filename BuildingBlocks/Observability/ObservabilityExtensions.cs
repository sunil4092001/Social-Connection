using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace BuildingBlocks.Observability;

/// <summary>
/// Distributed tracing with OpenTelemetry. Incoming requests, outgoing HTTP calls between services, and Kafka
/// publish/consume are traced; the W3C trace context travels in HTTP headers (YARP forwards it) and in Kafka
/// message headers (see <see cref="MessagingTracing"/>), so one trace follows a request across services.
/// Traces are exported only when OTEL_EXPORTER_OTLP_ENDPOINT is set (Jaeger in docker-compose).
/// </summary>
public static class ObservabilityExtensions
{
    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder, string serviceName)
    {
        var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation(options =>
                        // Health probes run every few seconds and would drown the real traffic.
                        options.Filter = context => !context.Request.Path.StartsWithSegments("/health"))
                    .AddHttpClientInstrumentation()
                    .AddSource(MessagingTracing.SourceName);

                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    tracing.AddOtlpExporter();
                }
            });

        return builder;
    }
}

/// <summary>Spans and trace-context propagation for Kafka messages.</summary>
public static class MessagingTracing
{
    public const string SourceName = "BuildConnect.Messaging";
    public const string TraceParentHeader = "traceparent";
    private static readonly ActivitySource Source = new(SourceName);

    /// <summary>The current trace id in W3C format, stored with outbox events so publishing joins the original trace.</summary>
    public static string? CurrentTraceParent() => Activity.Current?.Id;

    public static Activity? StartPublish(string topic, string? parentTraceParent) =>
        Source.StartActivity($"{topic} publish", ActivityKind.Producer, ParseParent(parentTraceParent) ?? default)
            ?.SetTag("messaging.system", "kafka")
            .SetTag("messaging.destination.name", topic);

    public static Activity? StartConsume(string topic, string? parentTraceParent) =>
        Source.StartActivity($"{topic} process", ActivityKind.Consumer, ParseParent(parentTraceParent) ?? default)
            ?.SetTag("messaging.system", "kafka")
            .SetTag("messaging.destination.name", topic);

    private static ActivityContext? ParseParent(string? traceParent) =>
        !string.IsNullOrWhiteSpace(traceParent) && ActivityContext.TryParse(traceParent, null, out var context) ? context : null;
}
