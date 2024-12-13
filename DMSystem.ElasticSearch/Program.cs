using DMSystem.ElasticSearch;

var builder = Host.CreateApplicationBuilder(args);

// Load configuration from appsettings.json or environment variables
builder.Configuration.SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("/app/config/appsettings.json", optional: false, reloadOnChange: true);

// Configure Elasticsearch settings
var elasticsearchUrl = builder.Configuration.GetValue<string>("ElasticSearch:Url");
if (string.IsNullOrWhiteSpace(elasticsearchUrl))
{
    throw new ArgumentNullException(nameof(elasticsearchUrl), "Elasticsearch URL is not configured. Ensure it is set in appsettings.json or as an environment variable.");
}
builder.Services.AddSingleton<IElasticSearchService>(new ElasticSearchService(elasticsearchUrl));

// Configure RabbitMQ using the new RabbitMQSettings
builder.Services.Configure<RabbitMQSettings>(builder.Configuration.GetSection("RabbitMQ"));
builder.Services.AddSingleton<IRabbitMQService, RabbitMQService>();

// Register the worker service
builder.Services.AddHostedService<Worker>();

// Add logging
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
});

// Build and run the application
var host = builder.Build();
await host.RunAsync();
