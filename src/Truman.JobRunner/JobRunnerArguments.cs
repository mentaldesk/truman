namespace Truman.JobRunner;

internal sealed record JobRunnerArguments(bool IsScheduled, bool RunMigrations, bool Fetch, bool Analyse, int? Limit, bool Samples = false)
{
    public static JobRunnerArguments Parse(string[] args)
    {
        int? limit = null;
        var limitIndex = Array.IndexOf(args, "--limit");
        if (limitIndex >= 0 && limitIndex + 1 < args.Length && int.TryParse(args[limitIndex + 1], out var parsedLimit))
        {
            limit = parsedLimit;
        }

        var runMigrations = args.Contains("--run-migrations");
        var fetch = args.Contains("--fetch");
        var analyse = args.Contains("--analyse");
        var samples = args.Contains("--samples");
        var fetchAndAnalyse = !runMigrations && !fetch && !analyse && !samples;

        return new JobRunnerArguments(
            IsScheduled: args.Length == 0,
            runMigrations,
            fetch || fetchAndAnalyse,
            analyse || fetchAndAnalyse,
            limit,
            samples);
    }
}
