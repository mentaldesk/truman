using DotNetEnv;
using DotNetEnv.Configuration;
using Microsoft.EntityFrameworkCore;
using Truman.Api.Features.Email;
using Truman.Api.Features.Articles;
using Truman.Api.Features.Profile;
using Truman.Api.Features.TagPreferences;
using Truman.Data;

var builder = WebApplication.CreateBuilder(args);

#if DEBUG
// Add a dotnet-env configuration source to bind to the `.env` file when developing locally.
// NOTE: When running in k8s / production, the .env file won't be present and these values must be
// read from the environment instead.
// 1. The env file gets loaded by the `k8s-create-secret` task as secrets into k8s.
// 2. Those secrets get added as environment variables via an `envFrom` in the deployment manifest.
builder.Configuration.AddDotNetEnv(".env", LoadOptions.TraversePath());
#endif

// Add database context
var connectionString = builder.Configuration.GetPostgresConnectionString();
builder.Services.AddDbContext<TrumanDbContext>(options => options.UseNpgsql(connectionString));

// Add Sentry for diagnostics
builder.WebHost.UseSentry(options =>
{
#if DEBUG
    options.Debug = true;
#endif
    options.Dsn = builder.Configuration["Sentry:Dsn"];
    options.Environment = builder.Environment.EnvironmentName;
    options.TracesSampleRate = 1.0; // Adjust as needed
    options.CaptureBlockingCalls = true;
    options.CaptureFailedRequests = true;
    options.SendDefaultPii = true;
    options.StackTraceMode = StackTraceMode.Enhanced;
});
builder.Services.AddSentryTunneling();

// Add services
builder.Services.AddAuthServices(builder.Configuration);
builder.Services.Configure<EmailConfiguration>(builder.Configuration.GetSection("Email"));
builder.Services.Configure<FrontendConfiguration>(builder.Configuration.GetSection("Frontend"));
// Configure Brevo transactional email API client with isolated configuration instance
builder.Services.AddSingleton<brevo_csharp.Api.ITransactionalEmailsApi>(sp => {
    var opts = sp.GetRequiredService<IOptions<EmailConfiguration>>();
    var cfg = new brevo_csharp.Client.Configuration(); // fresh instance, avoids static global state
    var apiKey = opts.Value.Brevo.ApiKey;
    if (!string.IsNullOrWhiteSpace(apiKey))
    {
        cfg.ApiKey["api-key"] = apiKey;
    }
    return new brevo_csharp.Api.TransactionalEmailsApi(cfg);
});
builder.Services.AddSingleton<IEmailService, EmailService>();
builder.Services.AddScoped<IRelevantArticlesService, RelevantArticlesService>();
builder.Services.AddScoped<ITagPreferenceService, TagPreferenceService>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddOpenApi();
builder.Services.AddHttpClient();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var frontendConfig = builder.Configuration.GetSection("Frontend").Get<FrontendConfiguration>();
        // For debug purposes
        Console.WriteLine("Frontend.BaseUrl = {0}", frontendConfig?.BaseUrl);
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
app.MapTagPreferenceEndpoints();
app.UseSentryTunneling();

var target = Environment.GetEnvironmentVariable("TARGET") ?? "World";
var port = Environment.GetEnvironmentVariable("PORT") ?? "5001";
var url = $"http://0.0.0.0:{port}";

app.MapGet("/", () => $"Hello {target}!");

app.Run(url);