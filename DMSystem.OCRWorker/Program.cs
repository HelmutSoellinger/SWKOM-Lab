using DMSystem.Messaging;
using DMSystem.Minio;
using DMSystem.OCRWorker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// Add configuration from the shared volume
builder.Configuration.SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("/app/config/appsettings.json", optional: false, reloadOnChange: true);

// Register RabbitMQ settings
builder.Services.Configure<RabbitMQSetting>(builder.Configuration.GetSection("RabbitMQ"));

// Register MinIO settings
builder.Services.Configure<MinioSettings>(builder.Configuration.GetSection("Minio"));

// Register MinIO FileStorage Service
builder.Services.AddSingleton<MinioFileStorageService>();

// Register Worker Service
builder.Services.AddHostedService<DMSystem.OCRWorker.Worker>();

// Add logging for better observability
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
});

// Build and run the application
var host = builder.Build();
await host.RunAsync();
