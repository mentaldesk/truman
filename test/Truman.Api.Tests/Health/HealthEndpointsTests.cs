using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Truman.Api.Features.Health;
using Truman.Data;

namespace Truman.Api.Tests.Health;

[Collection("Database collection")]
public class HealthEndpointsTests
{
    private readonly DbContextOptions<TrumanDbContext> _dbOptions;

    public HealthEndpointsTests(DatabaseFixture fixture)
    {
        _dbOptions = fixture.DbOptions;
    }

    [Fact]
    public void Status_ResponseShape_IncludesEnvironment()
    {
        var response = new HealthEndpoints.HealthResponse("ok", "Test");

        Assert.Equal("ok", response.Status);
        Assert.Equal("Test", response.Environment);
    }

    [Fact]
    public async Task Ready_ReturnsOk_WhenDatabaseIsReachable()
    {
        await using var context = new TrumanDbContext(_dbOptions);
        var dbReady = await context.Database.CanConnectAsync(TestContext.Current.CancellationToken);
        var response = new HealthEndpoints.ReadinessResponse(
            dbReady ? "ok" : "degraded",
            "Test",
            new HealthEndpoints.DependencyChecks(dbReady ? "ok" : "fail"));

        Assert.True(dbReady);
        Assert.Equal("ok", response.Status);
        Assert.Equal("Test", response.Environment);
        Assert.Equal("ok", response.Checks.Db);
    }

    [Fact]
    public void Ready_ResponseShape_RepresentsDbFailure()
    {
        var response = new HealthEndpoints.ReadinessResponse(
            "degraded",
            "Production",
            new HealthEndpoints.DependencyChecks("fail"));

        var json = JsonSerializer.Serialize(response);

        Assert.Contains("\"status\":\"degraded\"", json);
        Assert.Contains("\"environment\":\"Production\"", json);
        Assert.Contains("\"checks\":{\"db\":\"fail\"}", json);
    }
}
