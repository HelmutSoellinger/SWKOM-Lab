using DMSystem.Minio;
using DMSystem.OCRWorker;

var builder = Host.CreateApplicationBuilder(args);

// Add configuration from the shared volume
builder.Configuration.SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("/app/config/appsettings.json", optional: false, reloadOnChange: true);

// Register RabbitMQ settings and service
builder.Services.Configure<RabbitMQSettings>(builder.Configuration.GetSection("RabbitMQ"));
builder.Services.AddSingleton<IRabbitMQService, RabbitMQService>(); // Ensure RabbitMQService is implemented as shown in previous examples.

// Register MinIO settings
builder.Services.Configure<MinioSettings>(builder.Configuration.GetSection("Minio"));

// Register MinIO FileStorage Service
builder.Services.AddSingleton<MinioFileStorageService>();

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
