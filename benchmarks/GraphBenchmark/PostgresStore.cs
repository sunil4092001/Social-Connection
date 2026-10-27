using System.Diagnostics;
using Npgsql;
using NpgsqlTypes;

namespace GraphBenchmark;

/// <summary>
/// The relational way to store a social graph: an adjacency table with both directions of every connection and a
/// primary key on (user_a, user_b), which is also the index used to follow connections. Multi-hop questions become
/// self-joins and a recursive CTE.
/// </summary>
public sealed class PostgresStore(NpgsqlDataSource dataSource) : IGraphStore
{
    public string Name => "PostgreSQL";

    public static async Task<PostgresStore> CreateAsync(string adminConnectionString, string database)
    {
        // Create the benchmark database if needed (never one of the application databases).
        await using (var admin = NpgsqlDataSource.Create(adminConnectionString))
        {
            var exists = await admin.CreateCommand($"SELECT 1 FROM pg_database WHERE datname = '{database}'").ExecuteScalarAsync();
            if (exists is null) await admin.CreateCommand($"CREATE DATABASE {database}").ExecuteNonQueryAsync();
        }

        var builder = new NpgsqlConnectionStringBuilder(adminConnectionString) { Database = database };
        return new PostgresStore(NpgsqlDataSource.Create(builder.ConnectionString));
    }

    public async Task<TimeSpan> LoadAsync(SocialGraph graph)
    {
        await ExecuteAsync("""
            DROP TABLE IF EXISTS bench_connections;
            DROP TABLE IF EXISTS bench_users;
            CREATE TABLE bench_users (id integer PRIMARY KEY);
            CREATE TABLE bench_connections (user_a integer NOT NULL, user_b integer NOT NULL, PRIMARY KEY (user_a, user_b));
            """);

        var stopwatch = Stopwatch.StartNew();
        await using var connection = await dataSource.OpenConnectionAsync();
        await using (var users = await connection.BeginBinaryImportAsync("COPY bench_users (id) FROM STDIN (FORMAT BINARY)"))
        {
            for (var id = 0; id < graph.UserCount; id++) await users.WriteRowAsync(default, id);
            await users.CompleteAsync();
        }
        await using (var edges = await connection.BeginBinaryImportAsync("COPY bench_connections (user_a, user_b) FROM STDIN (FORMAT BINARY)"))
        {
            foreach (var (a, b) in graph.Edges)
            {
                await edges.StartRowAsync(); await edges.WriteAsync(a, NpgsqlDbType.Integer); await edges.WriteAsync(b, NpgsqlDbType.Integer);
                await edges.StartRowAsync(); await edges.WriteAsync(b, NpgsqlDbType.Integer); await edges.WriteAsync(a, NpgsqlDbType.Integer);
            }
            await edges.CompleteAsync();
        }
        await ExecuteAsync("ANALYZE bench_users; ANALYZE bench_connections;");
        return stopwatch.Elapsed;
    }

    public async Task<IReadOnlyList<(int UserId, int Mutual)>> SuggestionsAsync(int userId)
    {
        await using var command = dataSource.CreateCommand("""
            SELECT f2.user_b AS candidate, COUNT(DISTINCT f1.user_b) AS mutual
            FROM bench_connections f1
            JOIN bench_connections f2 ON f2.user_a = f1.user_b
            WHERE f1.user_a = @id
              AND f2.user_b <> @id
              AND NOT EXISTS (SELECT 1 FROM bench_connections d WHERE d.user_a = @id AND d.user_b = f2.user_b)
            GROUP BY f2.user_b
            ORDER BY mutual DESC, candidate
            LIMIT 10
            """);
        command.Parameters.AddWithValue("id", userId);
        var result = new List<(int, int)>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) result.Add((reader.GetInt32(0), (int)reader.GetInt64(1)));
        return result;
    }

    public async Task<IReadOnlyList<int>> MutualAsync(int userId, int otherUserId)
    {
        await using var command = dataSource.CreateCommand("""
            SELECT a.user_b FROM bench_connections a
            JOIN bench_connections b ON b.user_b = a.user_b AND b.user_a = @b
            WHERE a.user_a = @a AND a.user_b <> @a AND a.user_b <> @b
            ORDER BY a.user_b
            """);
        command.Parameters.AddWithValue("a", userId);
        command.Parameters.AddWithValue("b", otherUserId);
        var result = new List<int>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) result.Add(reader.GetInt32(0));
        return result;
    }

    public async Task<IReadOnlyDictionary<int, int>> DegreesAsync(int userId, IReadOnlyList<int> candidateIds)
    {
        await using var command = dataSource.CreateCommand("""
            SELECT c.id,
                   CASE WHEN EXISTS (SELECT 1 FROM bench_connections d WHERE d.user_a = @id AND d.user_b = c.id) THEN 1 ELSE 2 END
            FROM unnest(@candidates) AS c(id)
            WHERE c.id <> @id
              AND (EXISTS (SELECT 1 FROM bench_connections d WHERE d.user_a = @id AND d.user_b = c.id)
                   OR EXISTS (SELECT 1 FROM bench_connections f1
                              JOIN bench_connections f2 ON f2.user_a = f1.user_b AND f2.user_b = c.id
                              WHERE f1.user_a = @id AND f1.user_b <> c.id))
            """);
        command.Parameters.AddWithValue("id", userId);
        command.Parameters.AddWithValue("candidates", candidateIds.ToArray());
        var result = new Dictionary<int, int>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) result[reader.GetInt32(0)] = reader.GetInt32(1);
        return result;
    }

    public async Task<long> ReachWithinThreeHopsAsync(int userId)
    {
        // UNION (not UNION ALL) removes repeated (user, depth) pairs while expanding.
        await using var command = dataSource.CreateCommand("""
            WITH RECURSIVE reach(id, depth) AS (
                SELECT @id, 0
                UNION
                SELECT c.user_b, r.depth + 1
                FROM reach r JOIN bench_connections c ON c.user_a = r.id
                WHERE r.depth < 3
            )
            SELECT COUNT(DISTINCT id) FROM reach WHERE id <> @id
            """);
        command.Parameters.AddWithValue("id", userId);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    public Task CleanUpAsync() => ExecuteAsync("DROP TABLE IF EXISTS bench_connections; DROP TABLE IF EXISTS bench_users;");

    private async Task ExecuteAsync(string sql)
    {
        await using var command = dataSource.CreateCommand(sql);
        await command.ExecuteNonQueryAsync();
    }

    public ValueTask DisposeAsync() => dataSource.DisposeAsync();
}
