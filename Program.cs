using System;
using Imagetextextraction.Backend.Data;
using Imagetextextraction.Backend.Hubs;
using Imagetextextraction.Backend.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});
builder.Services.AddMemoryCache();

// Enable SignalR
builder.Services.AddSignalR();

// Register DbContext with PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// Register HttpClient & GeminiService
builder.Services.AddHttpClient<GeminiService>();

// Configure CORS to allow frontend connections with credentials (mandatory for SignalR!)
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:3000") // React Vite / CRA ports
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Required for SignalR webSockets
    });
});

// Configure API Swagger/OpenAPI documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("CorsPolicy");

app.UseStaticFiles(); // Enable serving uploaded images

app.UseAuthorization();

app.MapControllers();

// Map SignalR Hub Endpoint
app.MapHub<PrescriptionHub>("/hubs/prescription");

// Map a basic test check
app.MapGet("/", () => "Image Text Generator .NET 8 Backend API is active.");

app.Run();
