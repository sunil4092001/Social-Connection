namespace GraphBenchmark;

/// <summary>
/// The four network questions BuildConnect asks (see ConnectionService), implemented once per database.
/// Both implementations must return identical answers; Program verifies this before timing anything.
/// </summary>
public interface IGraphStore : IAsyncDisposable
{
    string Name { get; }

    /// <summary>Removes previous benchmark data and loads the graph. Returns the load time.</summary>
    Task<TimeSpan> LoadAsync(SocialGraph graph);

    /// <summary>People you may know: users two steps away, not already connected, ranked by mutual connections (top 10).</summary>
    Task<IReadOnlyList<(int UserId, int Mutual)>> SuggestionsAsync(int userId);

    /// <summary>Connections that two users share.</summary>
    Task<IReadOnlyList<int>> MutualAsync(int userId, int otherUserId);

    /// <summary>For each candidate: 1 when connected, 2 when they share a connection; others are omitted.</summary>
    Task<IReadOnlyDictionary<int, int>> DegreesAsync(int userId, IReadOnlyList<int> candidateIds);

    /// <summary>How many distinct users are reachable within three steps.</summary>
    Task<long> ReachWithinThreeHopsAsync(int userId);

    /// <summary>Removes the benchmark data.</summary>
    Task CleanUpAsync();
}
