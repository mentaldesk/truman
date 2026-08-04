using DotNetEnv;
using DotNetEnv.Configuration;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Truman.Api.Features.Email;
using Truman.Api.Features.Articles;
using Truman.Api.Features.Feeds;
using Truman.Api.Features.Presenters;
using Truman.Api.Features.Profile;
using Truman.Api.Features.TagPreferences;
using Truman.Api.Features.Health;
using Truman.Data;
using System.Text.Json;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

#if DEBUG
builder.Configuration.AddDotNetEnv(".env", LoadOptions.TraversePath());
#endif

var connectionString = builder.Configuration.GetPostgresConnectionString();
builder.Services.AddDbContext<TrumanDbContext>(options => options.UseNpgsql(connectionString));

// A sample rate of 1.0 records every trace. That is what you want while you are looking for
// one, and not what you want once you are paying to store them, so it is a development
// default rather than a universal one. Override per deployment with Sentry:TracesSampleRate.
// The browser is handed this same value via /config.js so the two ends of a distributed
// trace cannot drift apart.
var tracesSampleRate = builder.Configuration.GetValue<double?>("Sentry:TracesSampleRate")
                       ?? (builder.Environment.IsDevelopment() ? 1.0 : 0.2);

builder.WebHost.UseSentry(options =>
{
#if DEBUG
    options.Debug = true;
#endif
    options.Dsn = builder.Configuration["Sentry:Dsn"];
    // Environment is deliberately not set here. Sentry.AspNetCore derives it from
    // ASPNETCORE_ENVIRONMENT, mapping the standard names onto Sentry's lower-case convention
    // (Production -> "production", Staging -> "staging", Development -> "development") and
    // passing custom names through verbatim. Assigning it here runs *before* that mapping and
    // suppresses it, and also blocks SENTRY_ENVIRONMENT as an override.
    options.TracesSampleRate = tracesSampleRate;
    options.CaptureBlockingCalls = true;
    options.CaptureFailedRequests = true;
    options.SendDefaultPii = true;
    options.StackTraceMode = StackTraceMode.Enhanced;
});
builder.Services.AddSentryTunneling();
builder.Host.AddSentryMetricsExporter();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddAuthServices(builder.Configuration);
builder.Services.Configure<EmailConfiguration>(builder.Configuration.GetSection("Email"));
builder.Services.Configure<FrontendConfiguration>(builder.Configuration.GetSection("Frontend"));
builder.Services.AddSingleton<brevo_csharp.Api.ITransactionalEmailsApi>(sp => {
    var opts = sp.GetRequiredService<IOptions<EmailConfiguration>>();
    var cfg = new brevo_csharp.Client.Configuration();
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
        Console.WriteLine("Frontend.BaseUrl = {0}", frontendConfig?.BaseUrl);
        var frontendUrl = frontendConfig?.BaseUrl ?? "http://localhost:3000";
        policy.WithOrigins(frontendUrl)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

var app = builder.Build();

app.UseForwardedHeaders();

app.MapOpenApi();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", "v1");
});

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

var candidateWebBuildPaths = new[]
{
    Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "web", "build")),
    Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "web", "build"))
};

var webBuildPath = candidateWebBuildPaths.FirstOrDefault(Directory.Exists);
var hasWebBuild = webBuildPath is not null;

if (hasWebBuild)
{
    var webBuildFileProvider = new PhysicalFileProvider(webBuildPath);

    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = webBuildFileProvider
    });

    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = webBuildFileProvider
    });

    app.MapGet("/config.js", (
        HttpContext context,
        IConfiguration configuration,
        IOptions<SentryAspNetCoreOptions> sentryOptions) =>
    {
        var apiUrl = context.Request.GetBaseUrl();
        var sentryDsn = configuration["Sentry:Dsn"] ?? string.Empty;
        // Hand the browser the environment the Sentry SDK actually resolved, not the raw
        // ASP.NET Core name. The two differ in casing ("Staging" vs "staging"), and Sentry
        // treats those as separate environments — which would split browser events away from
        // the API events they belong with.
        var environment = sentryOptions.Value.Environment ?? app.Environment.EnvironmentName;
        var socialEnabled = configuration.GetValue("Authentication:Social:Enabled", defaultValue: true);

        var js = string.Join("\n",
            "window.__API_URL__ = " + JsonSerializer.Serialize(apiUrl) + ";",
            "window.__ENVIRONMENT__ = " + JsonSerializer.Serialize(environment) + ";",
            "window.__SENTRY_DSN__ = " + JsonSerializer.Serialize(sentryDsn) + ";",
            "window.__SENTRY_TRACES_SAMPLE_RATE__ = " + JsonSerializer.Serialize(tracesSampleRate) + ";",
            "window.__SOCIAL_AUTH_ENABLED__ = " + JsonSerializer.Serialize(socialEnabled) + ";");

        return Results.Text(js, "application/javascript");
    }).ExcludeFromDescription();
}

app.MapHealthEndpoints();
app.MapAuthEndpoints();
app.MapArticleEndpoints();
app.MapProfileEndpoints();
app.MapTagPreferenceEndpoints();
app.MapFeedEndpoints();
app.MapPresenterEndpoints();
app.UseSentryTunneling();

if (hasWebBuild)
{
    app.MapFallback(async context =>
    {
        if (context.Request.Path.StartsWithSegments("/api") ||
            context.Request.Path.StartsWithSegments("/auth") ||
            context.Request.Path.StartsWithSegments("/openapi") ||
            context.Request.Path.StartsWithSegments("/swagger") ||
            context.Request.Path.StartsWithSegments("/config.js") ||
            context.Request.Path.StartsWithSegments("/sentry-tunnel"))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.SendFileAsync(Path.Combine(webBuildPath, "index.html"));
    }).ExcludeFromDescription();
}

var port = Environment.GetEnvironmentVariable("PORT") ?? "5001";
var url = $"http://0.0.0.0:{port}";

app.Run(url);
