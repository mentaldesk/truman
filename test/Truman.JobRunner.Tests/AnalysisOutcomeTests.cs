namespace Truman.JobRunner.Tests;

public class AnalysisOutcomeTests
{
    [Fact]
    public void ThrowIfNothingSucceeded_throws_when_every_attempt_failed()
    {
        var outcome = new AnalysisOutcome(Attempted: 3, Succeeded: 0, Failed: 3);

        var ex = Assert.Throws<InvalidOperationException>(outcome.ThrowIfNothingSucceeded);
        Assert.Contains("all 3 articles", ex.Message);
    }

    [Fact]
    public void ThrowIfNothingSucceeded_is_silent_when_nothing_was_attempted()
    {
        new AnalysisOutcome(0, 0, 0).ThrowIfNothingSucceeded();
    }

    [Theory]
    [InlineData(1, 1, 0)]
    [InlineData(3, 1, 2)]
    [InlineData(3, 3, 0)]
    public void ThrowIfNothingSucceeded_is_silent_when_anything_succeeded(int attempted, int succeeded, int failed)
    {
        new AnalysisOutcome(attempted, succeeded, failed).ThrowIfNothingSucceeded();
    }

    [Theory]
    [InlineData(3, 0, 3, true)]
    [InlineData(0, 0, 0, false)]
    [InlineData(3, 1, 2, false)]
    public void NothingSucceeded_is_true_only_when_every_attempt_failed(int attempted, int succeeded, int failed, bool expected)
    {
        Assert.Equal(expected, new AnalysisOutcome(attempted, succeeded, failed).NothingSucceeded);
    }
}
