using System.Diagnostics;
using BuildingBlocks.Observability;

namespace LinkedIn.Tests;

/// <summary>One trace id must survive request -> outbox (stored traceparent) -> Kafka publish -> consume.</summary>
public sealed class TracingTests : IDisposable
{
    private readonly ActivityListener listener = new()
    {
        ShouldListenTo = _ => true,
        Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
    };

    public TracingTests() => ActivitySource.AddActivityListener(listener);

    [Fact]
    public void Publish_and_consume_continue_the_trace_of_the_original_request()
    {
        using var request = new Activity("POST /api/requirements").Start();
        var storedInOutbox = MessagingTracing.CurrentTraceParent();
        request.Stop();

        // Later, on the outbox relay thread, there is no current activity: the stored value links the trace.
        Assert.Null(Activity.Current);
        using var publish = MessagingTracing.StartPublish("requirement-created", storedInOutbox);
        var header = publish!.Id;
        publish.Stop();

        using var consume = MessagingTracing.StartConsume("requirement-created", header);

        Assert.Equal(request.TraceId, publish.TraceId);
        Assert.Equal(request.TraceId, consume!.TraceId);
        Assert.Equal(ActivityKind.Producer, publish.Kind);
        Assert.Equal(ActivityKind.Consumer, consume.Kind);
    }

    [Fact]
    public void Missing_or_invalid_trace_header_starts_a_new_trace()
    {
        using var consume = MessagingTracing.StartConsume("post-liked", "not-a-traceparent");

        Assert.NotNull(consume);
        Assert.Equal(default, consume.ParentSpanId);
    }

    public void Dispose() => listener.Dispose();
}
