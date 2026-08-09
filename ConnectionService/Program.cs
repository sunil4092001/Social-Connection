using BuildingBlocks.ExceptionHandling;
using BuildingBlocks.Logging;
using ConnectionService.Application;
using ConnectionService.Application.Interfaces;
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

builder.Services.Configure<Neo4jOptions>(builder.Configuration.GetSection("Neo4j"));
builder.Services.AddSingleton(GraphDatabase.Driver(
    builder.Configuration["Neo4j:Uri"],
    AuthTokens.Basic(builder.Configuration["Neo4j:Username"], builder.Configuration["Neo4j:Password"])));
builder.Services.AddScoped<IConnectionRepository, Neo4jConnectionRepository>();
builder.Services.AddMediatR(configuration => configuration.RegisterServicesFromAssembly(typeof(Program).Assembly));
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
builder.AddSerilogLogging("ConnectionService");

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseHttpsRedirection();
app.MapControllers();
app.Run();

public partial class Program;
