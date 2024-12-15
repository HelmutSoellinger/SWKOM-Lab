using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using AutoMapper;
using FluentValidation.AspNetCore;
using log4net;
using log4net.Config;
using System.IO;
using System.Reflection;
using DMSystem.DAL;
using DMSystem.Mappings;
using DMSystem.Contracts.DTOs;
using DMSystem.Minio;
using DMSystem.Messaging;
using DMSystem.ElasticSearch;

var builder = WebApplication.CreateBuilder(args);

// Configure Log4Net for logging
var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
XmlConfigurator.Configure(logRepository, new FileInfo("log4net.config"));
var logger = LogManager.GetLogger(typeof(Program));
logger.Info("Initializing application...");

// Configure services

// Database context
builder.Services.AddDbContext<DALContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Document Repository
builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();

// AutoMapper profiles
builder.Services.AddAutoMapper(typeof(DocumentProfile).Assembly);

// Controllers and FluentValidation
builder.Services.AddControllers()
    .AddFluentValidation(fv => fv.RegisterValidatorsFromAssemblyContaining<DocumentDTOValidator>());

// Configure RabbitMQ
builder.Services.Configure<RabbitMQSettings>(builder.Configuration.GetSection("RabbitMQ"));
builder.Services.AddSingleton<IRabbitMQService, RabbitMQService>();

// Configure MinIO settings
builder.Services.Configure<MinioSettings>(builder.Configuration.GetSection("Minio"));

// Register MinIO FileStorage Service using the IFileStorageService interface
builder.Services.AddSingleton<IMinioFileStorageService, MinioFileStorageService>();

// Configure ElasticSearch
var elasticSearchUrl = builder.Configuration.GetValue<string>("ElasticSearch:Url");
if (string.IsNullOrWhiteSpace(elasticSearchUrl))
{
    throw new InvalidOperationException("ElasticSearch URL is not configured. Ensure it is set in appsettings.json or as an environment variable.");
}

// Register ElasticsearchClientWrapper as a singleton
builder.Services.AddSingleton<IElasticsearchClientWrapper>(provider =>
{
    return new ElasticsearchClientWrapper(elasticSearchUrl);
});

// Register ElasticSearchService
builder.Services.AddScoped<IElasticSearchService, ElasticSearchService>();

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
        var fileStorageService = scope.ServiceProvider.GetRequiredService<IMinioFileStorageService>();
        await fileStorageService.InitializeBucketAsync();
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
