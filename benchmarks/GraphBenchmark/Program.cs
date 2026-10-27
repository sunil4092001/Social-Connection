using System.Diagnostics;
using System.Globalization;
using System.Text;
using GraphBenchmark;

// Neo4j vs PostgreSQL on BuildConnect's network questions.
//
//   dotnet run -c Release --project benchmarks/GraphBenchmark -- --sizes 1000,10000 --runs 30
//
// Connection settings (environment variables, defaults suit the local development setup):
//   BENCH_NEO4J_URI (neo4j://localhost:7687)  BENCH_NEO4J_USER (neo4j)  BENCH_NEO4J_PASSWORD (required)
//   BENCH_NEO4J_DATABASE (benchmark)          BENCH_PG (Host=localhost;Username=postgres;Password=...)  BENCH_PG_DATABASE (graph_benchmark)
// The application databases (LinkedInDB, linkedin_*) are never touched.

var options = Options.Parse(args);
var culture = CultureInfo.InvariantCulture;

Console.WriteLine($"Graph benchmark: sizes {string.Join(", ", options.Sizes)}, {options.ConnectionsPerUser} new connections per user, {options.Runs} timed runs per query, seed {options.Seed}");

await using var neo4j = await Neo4jStore.CreateAsync(options.Neo4jUri, options.Neo4jUser, options.Neo4jPassword, options.Neo4jDatabase);
await using var postgres = await PostgresStore.CreateAsync(options.PostgresConnection, options.PostgresDatabase);
IGraphStore[] stores = [neo4j, postgres];

var rows = new List<ResultRow>();
foreach (var size in options.Sizes)
{
    var graph = SocialGraph.Generate(size, options.ConnectionsPerUser, options.Seed);
    Console.WriteLine();
    Console.WriteLine($"== {size:N0} users, {graph.Edges.Count:N0} connections (average {graph.AverageDegree:F1} per user)");

    foreach (var store in stores)
    {
        var loadTime = await store.LoadAsync(graph);
        Console.WriteLine($"  {store.Name,-10} loaded in {loadTime.TotalSeconds:F1} s");
        rows.Add(new ResultRow(size, graph.Edges.Count, store.Name, "load (s)", loadTime.TotalSeconds, loadTime.TotalSeconds, loadTime.TotalSeconds));
    }

    // The same randomly chosen users and candidates are used for both databases.
    var random = new Random(options.Seed + size);
    var samples = Enumerable.Range(0, options.Runs + options.Warmup).Select(_ => new Sample(
        random.Next(size),
        random.Next(size),
        Enumerable.Range(0, 50).Select(_ => random.Next(size)).Distinct().ToArray())).ToArray();

    await VerifySameAnswersAsync(neo4j, postgres, samples.Take(5).ToArray());

    var queries = new (string Name, Func<IGraphStore, Sample, Task> Run)[]
    {
        ("suggestions (2 hops, top 10)", (store, s) => store.SuggestionsAsync(s.User)),
        ("mutual connections", (store, s) => store.MutualAsync(s.User, s.Other)),
        ("degrees of 50 users", (store, s) => store.DegreesAsync(s.User, s.Candidates)),
        ("reach within 3 hops", (store, s) => store.ReachWithinThreeHopsAsync(s.User)),
    };

    foreach (var (name, run) in queries)
    {
        foreach (var store in stores)
        {
            var timings = new List<double>();
            for (var i = 0; i < samples.Length; i++)
            {
                var stopwatch = Stopwatch.StartNew();
                await run(store, samples[i]);
                stopwatch.Stop();
                if (i >= options.Warmup) timings.Add(stopwatch.Elapsed.TotalMilliseconds);
            }
            timings.Sort();
            var row = new ResultRow(size, graph.Edges.Count, store.Name, name, Percentile(timings, 0.5), Percentile(timings, 0.95), timings.Average());
            rows.Add(row);
            Console.WriteLine($"  {store.Name,-10} {name,-30} median {row.Median,9:F2} ms   p95 {row.P95,9:F2} ms");
        }
    }
}

if (!options.KeepData)
{
    foreach (var store in stores) await store.CleanUpAsync();
}

var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", culture);
var resultsDirectory = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "results");
Directory.CreateDirectory(resultsDirectory);
var csvPath = Path.GetFullPath(Path.Combine(resultsDirectory, $"graph-benchmark-{stamp}.csv"));
var markdownPath = Path.ChangeExtension(csvPath, ".md");
await File.WriteAllTextAsync(csvPath, ToCsv(rows));
await File.WriteAllTextAsync(markdownPath, ToMarkdown(rows, options));
Console.WriteLine();
Console.WriteLine($"Results: {csvPath}");
Console.WriteLine($"         {markdownPath}");
return;

static async Task VerifySameAnswersAsync(IGraphStore a, IGraphStore b, Sample[] samples)
{
    foreach (var s in samples)
    {
        Check("suggestions", (await a.SuggestionsAsync(s.User)).SequenceEqual(await b.SuggestionsAsync(s.User)));
        Check("mutual", (await a.MutualAsync(s.User, s.Other)).SequenceEqual(await b.MutualAsync(s.User, s.Other)));
        var degreesA = await a.DegreesAsync(s.User, s.Candidates);
        var degreesB = await b.DegreesAsync(s.User, s.Candidates);
        Check("degrees", degreesA.Count == degreesB.Count && degreesA.All(kv => degreesB.TryGetValue(kv.Key, out var v) && v == kv.Value));
        Check("reach", await a.ReachWithinThreeHopsAsync(s.User) == await b.ReachWithinThreeHopsAsync(s.User));
    }
    Console.WriteLine($"  Answers identical in {a.Name} and {b.Name} for {samples.Length} sample users");

    static void Check(string query, bool same)
    {
        if (!same) throw new InvalidOperationException($"Neo4j and PostgreSQL returned different results for '{query}'; the benchmark would not be comparing like with like.");
    }
}

static double Percentile(List<double> sorted, double p)
{
    if (sorted.Count == 0) return 0;
    var index = (int)Math.Ceiling(p * sorted.Count) - 1;
    return sorted[Math.Clamp(index, 0, sorted.Count - 1)];
}

static string ToCsv(IEnumerable<ResultRow> rows)
{
    var csv = new StringBuilder("users,connections,database,query,median_ms,p95_ms,mean_ms\n");
    foreach (var r in rows)
        csv.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{r.Users},{r.Connections},{r.Database},\"{r.Query}\",{r.Median:F3},{r.P95:F3},{r.Mean:F3}"));
    return csv.ToString();
}

static string ToMarkdown(List<ResultRow> rows, Options options)
{
    var md = new StringBuilder();
    md.AppendLine("# Graph benchmark: Neo4j vs PostgreSQL");
    md.AppendLine();
    md.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{DateTime.Now:yyyy-MM-dd HH:mm} · {options.ConnectionsPerUser} new connections per user (preferential attachment) · {options.Runs} timed runs after {options.Warmup} warm-up · seed {options.Seed}. Times in milliseconds."));
    md.AppendLine();
    foreach (var size in rows.Select(r => r.Users).Distinct())
    {
        var connections = rows.First(r => r.Users == size).Connections;
        md.AppendLine(string.Create(CultureInfo.InvariantCulture, $"## {size:N0} users, {connections:N0} connections"));
        md.AppendLine();
        md.AppendLine("| Query | Neo4j median | Neo4j p95 | PostgreSQL median | PostgreSQL p95 | Faster |");
        md.AppendLine("|---|---:|---:|---:|---:|---|");
        foreach (var query in rows.Where(r => r.Users == size).Select(r => r.Query).Distinct())
        {
            var n = rows.Single(r => r.Users == size && r.Query == query && r.Database == "Neo4j");
            var p = rows.Single(r => r.Users == size && r.Query == query && r.Database == "PostgreSQL");
            var faster = n.Median < p.Median ? $"Neo4j ×{p.Median / Math.Max(n.Median, 0.001):F1}" : $"PostgreSQL ×{n.Median / Math.Max(p.Median, 0.001):F1}";
            md.AppendLine(string.Create(CultureInfo.InvariantCulture, $"| {query} | {n.Median:F2} | {n.P95:F2} | {p.Median:F2} | {p.P95:F2} | {faster} |"));
        }
        md.AppendLine();
    }
    return md.ToString();
}

internal sealed record Sample(int User, int Other, int[] Candidates);
internal sealed record ResultRow(int Users, int Connections, string Database, string Query, double Median, double P95, double Mean);

internal sealed record Options(
    int[] Sizes, int ConnectionsPerUser, int Runs, int Warmup, int Seed, bool KeepData,
    string Neo4jUri, string Neo4jUser, string Neo4jPassword, string Neo4jDatabase,
    string PostgresConnection, string PostgresDatabase)
{
    public static Options Parse(string[] args)
    {
        string? Arg(string name) { var i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
        string Env(string name, string? fallback = null) =>
            Environment.GetEnvironmentVariable(name) ?? fallback ?? throw new InvalidOperationException($"Set the {name} environment variable.");

        return new Options(
            Sizes: (Arg("--sizes") ?? "1000,10000").Split(',').Select(int.Parse).ToArray(),
            ConnectionsPerUser: int.Parse(Arg("--connections") ?? "10"),
            Runs: int.Parse(Arg("--runs") ?? "30"),
            Warmup: int.Parse(Arg("--warmup") ?? "5"),
            Seed: int.Parse(Arg("--seed") ?? "42"),
            KeepData: args.Contains("--keep-data"),
            Neo4jUri: Env("BENCH_NEO4J_URI", "neo4j://localhost:7687"),
            Neo4jUser: Env("BENCH_NEO4J_USER", "neo4j"),
            Neo4jPassword: Env("BENCH_NEO4J_PASSWORD"),
            Neo4jDatabase: Env("BENCH_NEO4J_DATABASE", "benchmark"),
            PostgresConnection: Env("BENCH_PG"),
            PostgresDatabase: Env("BENCH_PG_DATABASE", "graph_benchmark"));
    }
}
