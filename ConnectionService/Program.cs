using BuildingBlocks.Health;
using BuildingBlocks.Observability;
using BuildingBlocks.Authentication;
using BuildingBlocks.ExceptionHandling;
using BuildingBlocks.Logging;
using BuildingBlocks.Messaging;
using ConnectionService.Application;
using ConnectionService.Application.Interfaces;
using ConnectionService.Infrastructure;
using ConnectionService.Infrastructure.Outbox;
using ConnectionService.Infrastructure.Persistence;
using FluentValidation;
using MediatR;
using Microsoft.OpenApi.Models;
using Neo4j.Driver;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Connection Service API", Version = "v1" }));

// Jwt Configuration
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.Configure<Neo4jOptions>(builder.Configuration.GetSection("Neo4j"));
builder.Services.AddSingleton(GraphDatabase.Driver(
    builder.Configuration["Neo4j:Uri"],
    AuthTokens.Basic(builder.Configuration["Neo4j:Username"], builder.Configuration["Neo4j:Password"])));
builder.Services.AddScoped<IConnectionRepository, Neo4jConnectionRepository>();
builder.Services.AddHostedService<Neo4jSchemaInitializer>();

// Names and "does this user exist" come from UserService over HTTP (forwarding the caller's JWT).
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<IUserDirectoryClient, UserDirectoryClient>(client =>
    client.BaseAddress = new Uri(builder.Configuration["Services:UserService"]!));

// Events are stored as (:OutboxMessage) nodes with each graph change and relayed to Kafka.
builder.Services.AddKafkaEventPublisher();
builder.Services.AddHostedService<Neo4jOutboxPublisherService>();
builder.Services.AddMediatR(configuration => configuration.RegisterServicesFromAssembly(typeof(Program).Assembly));
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
builder.AddSerilogLogging("ConnectionService");
builder.AddObservability("ConnectionService");
builder.Services.AddHealthChecks().AddCheck<Neo4jHealthCheck>("neo4j", tags: [HealthEndpointExtensions.ReadyTag]);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<GlobalExceptionMiddleware>();
if (!app.Environment.IsDevelopment())
{
    // In development the gateway calls this service over http; redirecting would break proxied calls.
    app.UseHttpsRedirection();
}
// Without these two calls the JWT is never read and User.GetUserId() is always Guid.Empty.
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthEndpoints();
app.Run();

public partial class Program;
