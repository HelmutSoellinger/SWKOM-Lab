using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using AutoMapper;
using FluentValidation;
using FluentValidation.AspNetCore;
using log4net;
using log4net.Config;
using System.IO;
using System.Reflection;
using DMSystem.DAL;
using DMSystem.DAL.Models;
using DMSystem.Mappings;
using DMSystem.Messaging;
using DMSystem.DTOs;

var builder = WebApplication.CreateBuilder(args);

// Configure Log4Net for logging
var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
XmlConfigurator.Configure(logRepository, new FileInfo("log4net.config"));
var logger = LogManager.GetLogger(typeof(Program));
logger.Info("Initializing application...");

// Configure services
builder.Services.AddDbContext<DALContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))); // Database context

builder.Services.AddScoped<IDocumentRepository, DocumentRepository>(); // Document Repository
builder.Services.AddAutoMapper(typeof(DocumentProfile).Assembly); // AutoMapper profiles
builder.Services.AddControllers()
    .AddFluentValidation(fv => fv.RegisterValidatorsFromAssemblyContaining<DocumentDTOValidator>()); // FluentValidation
builder.Services.Configure<RabbitMQSetting>(builder.Configuration.GetSection("RabbitMQ")); // RabbitMQ settings
builder.Services.AddSingleton<IRabbitMQPublisher<OCRRequest>, RabbitMQPublisher<OCRRequest>>(); // RabbitMQ Publisher
builder.Services.AddHostedService<OrderValidationMessageConsumerService>(); // RabbitMQ Consumer
builder.Services.AddCors(options => // CORS policy
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});
builder.Services.AddEndpointsApiExplorer(); // API Explorer
builder.Services.AddSwaggerGen(c => // Swagger configuration
{
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath);
});

// Build the application
var app = builder.Build();

// Configure Middleware

// Enable Swagger in Development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Use HTTPS Redirection
app.UseHttpsRedirection();

// Apply CORS policy before Authorization middleware
app.UseCors("AllowAll");

// Authorization Middleware
app.UseAuthorization();

// Map Controllers
app.MapControllers();

// Health Check Endpoint
app.MapGet("/health", () => Results.Ok("Healthy")).WithTags("Health Check");

// Ensure Database Migration is Applied
using (var scope = app.Services.CreateScope())
{
    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<DALContext>();
        dbContext.Database.Migrate(); // Apply migrations
        logger.Info("Database migration completed successfully.");
    }
    catch (Exception ex)
    {
        logger.Error("An error occurred during database migration.", ex);
    }
}

// Log application start
logger.Info("Application has started.");

// Explicitly bind the application to 0.0.0.0:5000
app.Urls.Add("http://0.0.0.0:5000");

app.Run();
