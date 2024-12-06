using DMSystem.ElasticSearch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.IO;

var builder = Host.CreateApplicationBuilder(args);

// Add configuration from appsettings.json or environment variables
builder.Configuration.SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("/app/config/appsettings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables(); // Allow configuration overrides for containerized environments

// Configure Elasticsearch settings
var elasticsearchUrl = builder.Configuration.GetValue<string>("ElasticSearch:Url")
                       ?? Environment.GetEnvironmentVariable("ELASTICSEARCH_URL");

if (string.IsNullOrWhiteSpace(elasticsearchUrl))
{
    throw new ArgumentNullException(nameof(elasticsearchUrl), "Elasticsearch URL is not configured. Ensure the 'ElasticSearch:Url' key is set in appsettings.json or the 'ELASTICSEARCH_URL' environment variable is defined.");
}

// Log the Elasticsearch URL for debugging purposes
Console.WriteLine($"Using Elasticsearch URL: {elasticsearchUrl}");

// Register the Elasticsearch service
builder.Services.AddSingleton<IElasticSearchService>(new ElasticSearchService(elasticsearchUrl));

// Register the worker service
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
