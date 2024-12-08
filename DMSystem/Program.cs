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
using DMSystem.Mappings;
using DMSystem.Messaging;
using DMSystem.DTOs;
using DMSystem.Minio;
using DMSystem.ElasticSearch;

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

// Configure MinIO settings
builder.Services.Configure<MinioSettings>(builder.Configuration.GetSection("Minio")); // MinIO settings

// Add MinIO FileStorage Service
builder.Services.AddSingleton<MinioFileStorageService>();

// Configure ElasticSearch settings and service
var elasticSearchUrl = builder.Configuration.GetValue<string>("ElasticSearch:Url");
if (string.IsNullOrWhiteSpace(elasticSearchUrl))
{
    throw new InvalidOperationException("ElasticSearch URL is not configured. Ensure it is set in appsettings.json or as an environment variable.");
}
builder.Services.AddSingleton<IElasticSearchService>(new ElasticSearchService(elasticSearchUrl)); // ElasticSearch service

// CORS Policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add API Explorer and Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath);
});

// Build the application
var app = builder.Build();

// Configure Middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

// Health Check Endpoint
app.MapGet("/health", () => Results.Ok("Healthy")).WithTags("Health Check");

// Apply Database Migrations and Initialize Services
using (var scope = app.Services.CreateScope())
{
    try
    {
        // Apply database migrations
        var dbContext = scope.ServiceProvider.GetRequiredService<DALContext>();
        dbContext.Database.Migrate();
        logger.Info("Database migration completed successfully.");

        // Initialize MinIO bucket
        var minioService = scope.ServiceProvider.GetRequiredService<MinioFileStorageService>();
        await minioService.InitializeBucketAsync();
        logger.Info("MinIO bucket initialization completed.");
    }
    catch (Exception ex)
    {
        logger.Error("An error occurred during application initialization.", ex);
    }
}

// Log application start
logger.Info("Application has started.");

// Bind application to 0.0.0.0:5000
app.Urls.Add("http://0.0.0.0:5000");

app.Run();
