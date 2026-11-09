namespace Truman.JobRunner.Tests;

public class JobRunnerArgumentsTests
{
    [Fact]
    public void No_arguments_is_the_scheduled_run()
    {
        var parsed = JobRunnerArguments.Parse([]);

        Assert.True(parsed.IsScheduled);
        Assert.True(parsed.Fetch);
        Assert.True(parsed.Analyse);
        Assert.Null(parsed.Limit);
    }

    [Fact]
    public void Limit_alone_is_a_manual_fetch_and_analyse()
    {
        var parsed = JobRunnerArguments.Parse(["--limit", "3"]);

        Assert.False(parsed.IsScheduled);
        Assert.True(parsed.Fetch);
        Assert.True(parsed.Analyse);
        Assert.Equal(3, parsed.Limit);
    }

    [Theory]
    [InlineData("--fetch", true, false)]
    [InlineData("--analyse", false, true)]
    public void A_named_step_runs_only_that_step_and_is_not_scheduled(string flag, bool fetch, bool analyse)
    {
        var parsed = JobRunnerArguments.Parse([flag]);

        Assert.False(parsed.IsScheduled);
        Assert.Equal(fetch, parsed.Fetch);
        Assert.Equal(analyse, parsed.Analyse);
    }

    [Fact]
    public void Migrations_alone_neither_fetch_nor_analyse()
    {
        var parsed = JobRunnerArguments.Parse(["--run-migrations"]);

        Assert.False(parsed.IsScheduled);
        Assert.True(parsed.RunMigrations);
        Assert.False(parsed.Fetch);
        Assert.False(parsed.Analyse);
    }
}
