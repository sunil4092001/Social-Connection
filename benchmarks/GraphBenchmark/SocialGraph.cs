namespace GraphBenchmark;

/// <summary>
/// A synthetic professional network generated with preferential attachment (Barabási–Albert): each new user
/// connects to <c>m</c> existing users, chosen with probability proportional to how connected they already are.
/// This produces "hub" users with many connections, like real social networks. The same seed always produces the
/// same graph, so Neo4j and PostgreSQL are loaded with identical data.
/// </summary>
public sealed class SocialGraph
{
    public int UserCount { get; }
    /// <summary>Undirected edges, each stored once with A &lt; B.</summary>
    public IReadOnlyList<(int A, int B)> Edges { get; }

    private SocialGraph(int userCount, IReadOnlyList<(int A, int B)> edges)
    {
        UserCount = userCount;
        Edges = edges;
    }

    public double AverageDegree => 2.0 * Edges.Count / UserCount;

    public static SocialGraph Generate(int userCount, int connectionsPerNewUser, int seed)
    {
        var random = new Random(seed);
        var edges = new HashSet<(int, int)>();
        // Every edge endpoint is appended here, so picking a random entry picks a user proportionally to its degree.
        var endpoints = new List<int>();

        // Start with a small fully connected core so early users have someone to attach to.
        var core = Math.Min(connectionsPerNewUser + 1, userCount);
        for (var a = 0; a < core; a++)
        for (var b = a + 1; b < core; b++)
        {
            edges.Add((a, b));
            endpoints.Add(a);
            endpoints.Add(b);
        }

        for (var user = core; user < userCount; user++)
        {
            var targets = new HashSet<int>();
            while (targets.Count < connectionsPerNewUser)
            {
                targets.Add(endpoints[random.Next(endpoints.Count)]);
            }
            foreach (var target in targets)
            {
                edges.Add((Math.Min(user, target), Math.Max(user, target)));
                endpoints.Add(user);
                endpoints.Add(target);
            }
        }

        return new SocialGraph(userCount, edges.ToList());
    }
}
