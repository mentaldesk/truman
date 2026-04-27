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
using Truman.Data;
using System.Text.Json;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

#if DEBUG
builder.Configuration.AddDotNetEnv(".env", LoadOptions.TraversePath());
#endif

var connectionString = builder.Configuration.GetPostgresConnectionString();
builder.Services.AddDbContext<TrumanDbContext>(options => options.UseNpgsql(connectionString));

builder.WebHost.UseSentry(options =>
{
#if DEBUG
    options.Debug = true;
#endif
    options.Dsn = builder.Configuration["Sentry:Dsn"];
    options.Environment = builder.Environment.EnvironmentName;
    options.TracesSampleRate = 1.0;
    options.CaptureBlockingCalls = true;
    options.CaptureFailedRequests = true;
    options.SendDefaultPii = true;
    options.StackTraceMode = StackTraceMode.Enhanced;
});
builder.Services.AddSentryTunneling();

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

    app.MapGet("/config.js", (HttpContext context, IConfiguration configuration) =>
    {
        var apiUrl = context.Request.GetBaseUrl();
        var sentryDsn = configuration["Sentry:Dsn"] ?? string.Empty;
        var environment = app.Environment.EnvironmentName;
        var socialEnabled = configuration.GetValue("Authentication:Social:Enabled", defaultValue: true);

        var js = string.Join("\n",
            "window.__API_URL__ = " + JsonSerializer.Serialize(apiUrl) + ";",
            "window.__ENVIRONMENT__ = " + JsonSerializer.Serialize(environment) + ";",
            "window.__SENTRY_DSN__ = " + JsonSerializer.Serialize(sentryDsn) + ";",
            "window.__SOCIAL_AUTH_ENABLED__ = " + JsonSerializer.Serialize(socialEnabled) + ";");

        return Results.Text(js, "application/javascript");
    }).ExcludeFromDescription();
}

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

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).ExcludeFromDescription();

app.Run(url);
