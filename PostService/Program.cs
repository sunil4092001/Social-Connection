using BuildingBlocks.Authentication;
using BuildingBlocks.ExceptionHandling;
using BuildingBlocks.Logging;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using PostService.Application;
using PostService.Application.Behaviors;
using PostService.Application.Features.Posts.Client;
using PostService.Application.Interfaces;
using PostService.Infrastructure.EventBus;
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
builder.Services.AddScoped<IIntegrationEventPublisher, LoggingIntegrationEventPublisher>();
builder.Services.AddMediatR(configuration => configuration.RegisterServicesFromAssembly(typeof(Program).Assembly));
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));


builder.Services.AddHttpClient<IUserServiceClient, UserServiceClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:UserService"]);
});

builder.AddSerilogLogging("PostService");

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();
app.Run();

public partial class Program;
