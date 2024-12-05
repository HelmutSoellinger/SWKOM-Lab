using DMSystem.Messaging;
using DMSystem.OCRWorker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// Add configuration from the shared volume
builder.Configuration.SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("/app/config/appsettings.json", optional: false, reloadOnChange: true);

// Register Worker Service
builder.Services.AddHostedService<Worker>();

// Register RabbitMQ settings
builder.Services.Configure<RabbitMQSetting>(builder.Configuration.GetSection("RabbitMQ"));

// Add logging for better observability
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
});

// Build and run the application
var host = builder.Build();
await host.RunAsync();
