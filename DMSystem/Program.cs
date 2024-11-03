using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using DMSystem.DAL;
using DMSystem.DAL.Models;
using Microsoft.EntityFrameworkCore;
using AutoMapper;
using DMSystem.Mappings;  // Namespace for your AutoMapper profiles
using DMSystem.Messaging;
using DMSystem.DTOs;
using FluentValidation;
using Microsoft.AspNetCore.Identity;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<DALContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))); // Use your connection string

// Register the Document Repository
builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();

// Register AutoMapper and scan for profiles (such as DocumentProfile)
builder.Services.AddAutoMapper(typeof(DocumentProfile).Assembly);  // Register all profiles in the current assembly


// Add other services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath);
});

builder.Services.AddValidatorsFromAssemblyContaining<DocumentDTOValidator>();

// Configure CORS policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder =>
        {
            builder.AllowAnyOrigin()
                   .AllowAnyMethod()
                   .AllowAnyHeader();
        });
});


// Add RabbitMQ settings
builder.Services.Configure<RabbitMQSetting>(builder.Configuration.GetSection("RabbitMQ"));

// Register RabbitMQ publisher for Document messages
builder.Services.AddSingleton<IRabbitMQPublisher<Document>, RabbitMQPublisher<Document>>();
builder.Services.AddHostedService<OrderValidationMessageConsumerService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Ensure the database is created.
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<DALContext>();
    dbContext.Database.Migrate();
}

// Use CORS before Authorization
app.UseCors("AllowAll");

app.UseAuthorization();

app.MapControllers();

app.Run();
