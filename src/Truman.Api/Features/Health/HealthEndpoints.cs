using Microsoft.EntityFrameworkCore;
using Truman.Data;

namespace Truman.Api.Features.Health;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/status", (IHostEnvironment environment) =>
            Results.Ok(new HealthResponse("ok", environment.EnvironmentName)))
        .WithName("GetStatus")
        .WithTags("Health");

        app.MapGet("/ready", async (TrumanDbContext dbContext, IHostEnvironment environment, CancellationToken cancellationToken) =>
        {
            var dbReady = await dbContext.Database.CanConnectAsync(cancellationToken);
            var response = new ReadinessResponse(
                dbReady ? "ok" : "degraded",
                environment.EnvironmentName,
                new DependencyChecks(dbReady ? "ok" : "fail"));

            return dbReady
                ? Results.Ok(response)
                : Results.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable);
        })
        .WithName("GetReadiness")
        .WithTags("Health");

        return app;
    }

    public sealed record HealthResponse(string Status, string Environment);

    public sealed record ReadinessResponse(string Status, string Environment, DependencyChecks Checks);

    public sealed record DependencyChecks(string Db);
}
