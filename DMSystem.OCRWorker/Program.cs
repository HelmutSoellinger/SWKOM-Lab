using DMSystem.Messaging;
using DMSystem.OCRWorker;

var builder = Host.CreateApplicationBuilder(args);

// Add configuration from the DMSystem project
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetParent(AppContext.BaseDirectory)?.FullName ?? string.Empty)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

builder.Configuration.AddConfiguration(configuration);

// Register Worker Service
builder.Services.AddHostedService<Worker>();

// Register RabbitMQ settings
builder.Services.Configure<RabbitMQSetting>(builder.Configuration.GetSection("RabbitMQ"));

var host = builder.Build();
host.Run();
