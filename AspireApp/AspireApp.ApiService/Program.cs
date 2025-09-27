using Microsoft.OpenApi.Models;
using NLog;
using NLog.Extensions.Logging;
using NLog.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();

var logger = NLog.LogManager.Setup().LoadConfigurationFromSection(builder.Configuration).GetCurrentClassLogger();

// Ensure Host will use NLog
builder.Host.UseNLog();

builder.Services.AddSingleton(NLog.LogManager.LogFactory);
builder.Services.AddSingleton<Func<Type, NLog.ILogger>>(sp => (type) => NLog.LogManager.GetLogger(type.FullName!));
builder.Services.AddTransient(typeof(NLogLogger<>));

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Add services to the container.
builder.Services.AddProblemDetails();

builder.Services.AddLogging(b => b.ClearProviders().AddNLog(builder.Configuration));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "AspireApp", Version = "v1" });
});
var app = builder.Build();

try
{
    // Configure the HTTP request pipeline.
    app.UseCors("AllowAll");
    app.UseRouting();
    app.UseExceptionHandler();
    app.MapControllers();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.MapDefaultEndpoints();
    logger.Info("------------------------Applicaion Starting------------------------");
    app.Run();
}
catch (Exception ex)
{
    // Log startup errors
    logger.Error(ex, "Application stopped because of exception");
    throw;
}
finally
{
    // Ensure to flush and stop internal timers/threads before application exit
    NLog.LogManager.Shutdown();
}

public sealed class NLogLogger<T>
{
    public NLog.ILogger Logger { get; } = NLog.LogManager.GetLogger(typeof(T).FullName!);
}