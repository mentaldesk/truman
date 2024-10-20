var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var target = Environment.GetEnvironmentVariable("TARGET") ?? "World";
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
var url = $"http://0.0.0.0:{port}";

app.MapGet("/", () => $"Hello {target}!");

app.Run(url);