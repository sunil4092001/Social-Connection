using BuildingBlocks.Health;
using BuildingBlocks.Observability;
using BuildingBlocks.Persistence;
using BuildingBlocks.ExceptionHandling;
using BuildingBlocks.Logging;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using BuildingBlocks.Authentication;
using BuildingBlocks.Messaging;
using NotificationService.Application;
using NotificationService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Notification Service API", Version = "v1" }));

builder.Services.AddDbContext<NotificationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddScoped<INotificationRepository, EfNotificationRepository>();
// The producer is only used to park unprocessable events on the dead-letter topic.
builder.Services.AddKafkaEventPublisher();
builder.Services.AddHostedService<KafkaNotificationConsumer>();
builder.Services.AddMediatR(configuration => configuration.RegisterServicesFromAssembly(typeof(Program).Assembly));
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
builder.AddSerilogLogging("NotificationService");
builder.AddObservability("NotificationService");
builder.Services.AddHealthChecks().AddDbContextCheck<NotificationDbContext>("database", tags: [HealthEndpointExtensions.ReadyTag]);

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
await app.MigrateDatabaseIfConfiguredAsync<NotificationDbContext>();
app.Run();

public partial class Program;
