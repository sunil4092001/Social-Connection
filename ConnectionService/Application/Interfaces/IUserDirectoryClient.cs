namespace ConnectionService.Application.Interfaces;

/// <summary>Read-only access to UserService profiles (UserService owns names; the graph stores only ids).</summary>
public interface IUserDirectoryClient
{
    /// <summary>Display name of a user, or null when the user has no profile.</summary>
    Task<string?> GetDisplayNameAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Names for many users in one call. Users that cannot be resolved are left out.</summary>
    Task<IReadOnlyDictionary<Guid, string>> GetDisplayNamesAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken);
}
