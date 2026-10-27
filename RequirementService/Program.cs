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
using RequirementService.Application;
using RequirementService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Kafka is the only message broker in this solution (BuildingBlocks.Messaging).
builder.Services.AddControllers().AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer(); builder.Services.AddSwaggerGen();
builder.Services.AddCors(options => options.AddPolicy("AllowAngularDev", policy => policy.WithOrigins("http://localhost:4200", "http://127.0.0.1:4200").AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<IUserProfileClient, UserProfileClient>(client => client.BaseAddress = new Uri(builder.Configuration["Services:UserService"]!));
// Network trust bonus for matching; a short timeout so a slow ConnectionService never delays posting a job.
builder.Services.AddHttpClient<IConnectionNetworkClient, ConnectionNetworkClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:ConnectionService"]!);
    client.Timeout = TimeSpan.FromSeconds(3);
});
builder.Services.Configure<RequirementNotificationOptions>(builder.Configuration.GetSection(RequirementNotificationOptions.SectionName));
builder.Services.AddSingleton<IRequirementMatchingService, RequirementMatchingService>();
builder.Services.AddDbContext<RequirementDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<IRequirementRepository, EfRequirementRepository>();
// Reliable events: handlers stage events in the outbox table (same transaction), the relay sends them to Kafka.
builder.Services.AddEfOutbox<RequirementDbContext>();
builder.Services.AddMediatR(c => c.RegisterServicesFromAssembly(typeof(Program).Assembly)); builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly); builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
builder.AddSerilogLogging("RequirementService");
builder.AddObservability("RequirementService");
builder.Services.AddHealthChecks().AddDbContextCheck<RequirementDbContext>("database", tags: [HealthEndpointExtensions.ReadyTag]);
var app = builder.Build(); 

if (app.Environment.IsDevelopment()) 
{ 
    app.UseSwagger(); 
    app.UseSwaggerUI(); 
}

app.UseMiddleware<CorrelationIdMiddleware>(); 
app.UseMiddleware<GlobalExceptionMiddleware>(); 
app.UseCors("AllowAngularDev"); 
app.UseAuthentication(); 
app.UseAuthorization();
app.MapControllers();
app.MapHealthEndpoints();
await app.MigrateDatabaseIfConfiguredAsync<RequirementDbContext>();
app.Run();
public partial class Program;
