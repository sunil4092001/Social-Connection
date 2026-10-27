using System.Diagnostics;
using Neo4j.Driver;

namespace GraphBenchmark;

/// <summary>
/// Same model as ConnectionService: one undirected relationship per connection, queried without direction.
/// Uses its own label and relationship type so it can never mix with application data.
/// </summary>
public sealed class Neo4jStore(IDriver driver, string database) : IGraphStore
{
    private const int BatchSize = 5_000;

    public string Name => "Neo4j";

    public static async Task<Neo4jStore> CreateAsync(string uri, string user, string password, string database)
    {
        if (string.Equals(database, "LinkedInDB", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Refusing to run the benchmark in the application database LinkedInDB.");

        var driver = GraphDatabase.Driver(uri, AuthTokens.Basic(user, password));
        await driver.VerifyConnectivityAsync();

        // Neo4j Desktop (Enterprise developer licence) can create a separate database; Community cannot, so fall back
        // to the given database and rely on the dedicated BenchUser label.
        try
        {
            await using var system = driver.AsyncSession(o => o.WithDatabase("system"));
            await (await system.RunAsync($"CREATE DATABASE `{database}` IF NOT EXISTS WAIT")).ConsumeAsync();
        }
        catch (ClientException exception)
        {
            Console.WriteLine($"  (Neo4j could not create database '{database}': {exception.Message.Split('.')[0]}; using it if it exists)");
        }

        return new Neo4jStore(driver, database);
    }

    public async Task<TimeSpan> LoadAsync(SocialGraph graph)
    {
        await CleanUpAsync();
        await RunAsync("CREATE CONSTRAINT bench_user_id IF NOT EXISTS FOR (u:BenchUser) REQUIRE u.id IS UNIQUE");

        var stopwatch = Stopwatch.StartNew();
        foreach (var batch in Enumerable.Range(0, graph.UserCount).Chunk(BatchSize))
        {
            await RunAsync("UNWIND $ids AS id CREATE (:BenchUser {id: id})", new { ids = batch });
        }
        foreach (var batch in graph.Edges.Chunk(BatchSize))
        {
            await RunAsync("""
                UNWIND $edges AS edge
                MATCH (a:BenchUser {id: edge[0]}), (b:BenchUser {id: edge[1]})
                CREATE (a)-[:BENCH_KNOWS]->(b)
                """, new { edges = batch.Select(edge => new[] { edge.A, edge.B }).ToArray() });
        }
        return stopwatch.Elapsed;
    }

    public async Task<IReadOnlyList<(int UserId, int Mutual)>> SuggestionsAsync(int userId)
    {
        var records = await QueryAsync("""
            MATCH (me:BenchUser {id: $id})-[:BENCH_KNOWS]-(friend:BenchUser)-[:BENCH_KNOWS]-(candidate:BenchUser)
            WHERE candidate <> me AND NOT (me)-[:BENCH_KNOWS]-(candidate)
            RETURN candidate.id AS userId, count(DISTINCT friend) AS mutual
            ORDER BY mutual DESC, userId
            LIMIT 10
            """, new { id = userId });
        return records.Select(r => (r["userId"].As<int>(), r["mutual"].As<int>())).ToList();
    }

    public async Task<IReadOnlyList<int>> MutualAsync(int userId, int otherUserId)
    {
        var records = await QueryAsync("""
            MATCH (:BenchUser {id: $a})-[:BENCH_KNOWS]-(m:BenchUser)-[:BENCH_KNOWS]-(:BenchUser {id: $b})
            WHERE m.id <> $a AND m.id <> $b
            RETURN DISTINCT m.id AS id ORDER BY id
            """, new { a = userId, b = otherUserId });
        return records.Select(r => r["id"].As<int>()).ToList();
    }

    public async Task<IReadOnlyDictionary<int, int>> DegreesAsync(int userId, IReadOnlyList<int> candidateIds)
    {
        var records = await QueryAsync("""
            MATCH (me:BenchUser {id: $id})
            UNWIND $candidates AS candidateId
            MATCH (candidate:BenchUser {id: candidateId})
            WHERE candidate <> me
            OPTIONAL MATCH (me)-[direct:BENCH_KNOWS]-(candidate)
            OPTIONAL MATCH (me)-[:BENCH_KNOWS]-(m:BenchUser)-[:BENCH_KNOWS]-(candidate)
            WHERE m <> me AND m <> candidate
            WITH candidateId, count(DISTINCT direct) > 0 AS connected, count(DISTINCT m) AS mutual
            WHERE connected OR mutual > 0
            RETURN candidateId AS userId, CASE WHEN connected THEN 1 ELSE 2 END AS degree
            """, new { id = userId, candidates = candidateIds });
        return records.ToDictionary(r => r["userId"].As<int>(), r => r["degree"].As<int>());
    }

    public async Task<long> ReachWithinThreeHopsAsync(int userId)
    {
        var records = await QueryAsync("""
            MATCH (me:BenchUser {id: $id})-[:BENCH_KNOWS*1..3]-(other:BenchUser)
            WHERE other <> me
            RETURN count(DISTINCT other) AS reach
            """, new { id = userId });
        return records[0]["reach"].As<long>();
    }

    public async Task CleanUpAsync()
    {
        // Delete in batches so large graphs do not need one huge transaction.
        while (true)
        {
            var records = await WriteQueryAsync("MATCH (u:BenchUser) WITH u LIMIT 10000 DETACH DELETE u RETURN count(*) AS deleted", new { });
            if (records[0]["deleted"].As<long>() == 0) break;
        }
    }

    private async Task RunAsync(string query, object? parameters = null)
    {
        await using var session = driver.AsyncSession(o => o.WithDatabase(database));
        await session.ExecuteWriteAsync(async tx => await (await tx.RunAsync(query, parameters)).ConsumeAsync());
    }

    /// <summary>Read query in a read transaction (what the application's queries use).</summary>
    private async Task<List<IRecord>> QueryAsync(string query, object parameters)
    {
        await using var session = driver.AsyncSession(o => o.WithDatabase(database));
        return await session.ExecuteReadAsync(async tx => await (await tx.RunAsync(query, parameters)).ToListAsync());
    }

    private async Task<List<IRecord>> WriteQueryAsync(string query, object parameters)
    {
        await using var session = driver.AsyncSession(o => o.WithDatabase(database));
        return await session.ExecuteWriteAsync(async tx => await (await tx.RunAsync(query, parameters)).ToListAsync());
    }

    public ValueTask DisposeAsync() => driver.DisposeAsync();
}
