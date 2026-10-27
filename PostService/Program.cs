using BuildingBlocks.Health;
using BuildingBlocks.Observability;
using BuildingBlocks.Persistence;
using BuildingBlocks.Authentication;
using BuildingBlocks.ExceptionHandling;
using BuildingBlocks.Logging;
using BuildingBlocks.Messaging.Outbox;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using PostService.Application;
using PostService.Application.Behaviors;
using PostService.Application.Interfaces;
using PostService.Infrastructure.Clients;
using PostService.Infrastructure.Persistence;
using PostService.Infrastructure.Persistence.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Post Service API", Version = "v1" }));


builder.Services.AddJwtAuthentication(builder.Configuration);

builder.Services.AddDbContext<PostDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<IPostRepository, EfPostRepository>();
// Like/comment events: stored in OutboxMessages with each change, relayed to Kafka.
builder.Services.AddEfOutbox<PostDbContext>();
builder.Services.AddMediatR(configuration => configuration.RegisterServicesFromAssembly(typeof(Program).Assembly));
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));


builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<IUserServiceClient, UserServiceClient>(client =>
    client.BaseAddress = new Uri(builder.Configuration["Services:UserService"]!));
// The network feed needs the caller's connections.
builder.Services.AddHttpClient<IConnectionServiceClient, ConnectionServiceClient>(client =>
    client.BaseAddress = new Uri(builder.Configuration["Services:ConnectionService"]!));

builder.AddSerilogLogging("PostService");
builder.AddObservability("PostService");
builder.Services.AddHealthChecks().AddDbContextCheck<PostDbContext>("database", tags: [HealthEndpointExtensions.ReadyTag]);

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

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();
app.MapHealthEndpoints();
await app.MigrateDatabaseIfConfiguredAsync<PostDbContext>();
app.Run();

public partial class Program;
