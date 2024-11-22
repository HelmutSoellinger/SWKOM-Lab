using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using DMSystem.DAL;
using DMSystem.DAL.Models;
using Microsoft.EntityFrameworkCore;
using AutoMapper;
using DMSystem.Mappings; // Namespace for AutoMapper profiles
using DMSystem.Messaging;
using DMSystem.DTOs;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.Extensions.Hosting;
using log4net;
using log4net.Config;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Configure Log4Net
var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
XmlConfigurator.Configure(logRepository, new FileInfo("log4net.config"));

// Example Log4Net instance for logging
var logger = LogManager.GetLogger(typeof(Program));
logger.Info("Initializing application...");

// Configure database context
builder.Services.AddDbContext<DALContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register the Document Repository
builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();

// Register AutoMapper and scan for profiles (e.g., DocumentProfile)
builder.Services.AddAutoMapper(typeof(DocumentProfile).Assembly); // Registers all profiles in the assembly

// Register FluentValidation and add validators
builder.Services.AddControllers()
    .AddFluentValidation(fv => fv.RegisterValidatorsFromAssemblyContaining<DocumentDTOValidator>());

// Register RabbitMQ settings and services
builder.Services.Configure<RabbitMQSetting>(builder.Configuration.GetSection("RabbitMQ"));
builder.Services.AddSingleton<IRabbitMQPublisher<Document>, RabbitMQPublisher<Document>>();
builder.Services.AddHostedService<OrderValidationMessageConsumerService>();

// Configure Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath);
});

// Configure CORS policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Ensure the database is created and migrated
using (var scope = app.Services.CreateScope())
{
    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<DALContext>();
        dbContext.Database.Migrate();
        logger.Info("Database migration completed successfully.");
    }
    catch (Exception ex)
    {
        logger.Error("An error occurred during database migration.", ex);
    }
}

// Use CORS before Authorization
app.UseCors("AllowAll");

app.UseAuthorization();

app.MapControllers();

// Example usage of Log4Net in Program.cs
logger.Info("Application has started.");

app.Run();
