using BuildingBlocks.Health;
using BuildingBlocks.Observability;
using BuildingBlocks.Logging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularDev", policy =>
    {
        policy.WithOrigins("http://localhost:4200", "http://127.0.0.1:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(
        builder.Configuration.GetSection("ReverseProxy"));

builder.AddSerilogLogging("ApiGateway");
builder.AddObservability("ApiGateway");
builder.Services.AddHealthChecks();
var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseCors("AllowAngularDev");
app.UseSwagger();
app.UseSwaggerUI();

app.MapReverseProxy();
app.MapHealthEndpoints();
app.Run();
