using DotNetEnv;
using DotNetEnv.Configuration;
using Microsoft.EntityFrameworkCore;
using Truman.Api.Features.Email;
using Truman.Api.Features.Articles;
using Truman.Api.Features.Profile;
using Truman.Data;

var builder = WebApplication.CreateBuilder(args);

// Add dotnet-env configuration source to load from .env file
builder.Configuration.AddDotNetEnv(".env", LoadOptions.TraversePath());

// Add database context
var connectionString = builder.Configuration.GetPostgresConnectionString();
builder.Services.AddDbContext<TrumanDbContext>(options => options.UseNpgsql(connectionString));

// Add services
builder.Services.AddAuthServices(builder.Configuration);
builder.Services.Configure<EmailConfiguration>(builder.Configuration.GetSection("Email"));
builder.Services.Configure<FrontendConfiguration>(builder.Configuration.GetSection("Frontend"));
builder.Services.AddSingleton<IEmailService, EmailService>();
builder.Services.AddScoped<IRelevantArticlesService, RelevantArticlesService>();
builder.Services.AddOpenApi();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var frontendConfig = builder.Configuration.GetSection("Frontend").Get<FrontendConfiguration>();
        var frontendUrl = frontendConfig?.BaseUrl ?? "http://localhost:3000";
        policy.WithOrigins(frontendUrl)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

var app = builder.Build();

app.MapOpenApi();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", "v1");
});

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// Map endpoints
app.MapAuthEndpoints();
app.MapArticleEndpoints();
app.MapProfileEndpoints();

var target = Environment.GetEnvironmentVariable("TARGET") ?? "World";
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
var url = $"http://0.0.0.0:{port}";

app.MapGet("/", () => $"Hello {target}!");

app.Run(url);