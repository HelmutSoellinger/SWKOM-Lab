using DMSystem.Minio;
using DMSystem.OCRWorker;
using DMSystem.Messaging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

var builder = Host.CreateApplicationBuilder(args);

// Add configuration from the shared volume
builder.Configuration.SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("/app/config/appsettings.json", optional: false, reloadOnChange: true);

// Register RabbitMQ settings and service
builder.Services.Configure<RabbitMQSettings>(builder.Configuration.GetSection("RabbitMQ"));
builder.Services.AddSingleton<IRabbitMQService, RabbitMQService>();

// Register MinIO settings
builder.Services.Configure<MinioSettings>(builder.Configuration.GetSection("Minio"));

// Register MinIO FileStorage Service with interface mapping
builder.Services.AddSingleton<IMinioFileStorageService, MinioFileStorageService>();

// Register Worker Service
builder.Services.AddHostedService<Worker>();

// Add logging for better observability
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
});

// Build and run the application
var host = builder.Build();
await host.RunAsync();
