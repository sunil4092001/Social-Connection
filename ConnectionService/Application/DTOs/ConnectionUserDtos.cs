namespace ConnectionService.Application.DTOs;

/// <summary>Body of accept / reject: the user who sent the request. The addressee is always the signed-in user.</summary>
public sealed record RespondToConnectionRequest(Guid RequesterUserId);

/// <summary>A user in one of my lists (connections, received or sent requests). Since = when that relationship started.</summary>
public sealed record ConnectionUserDto(Guid UserId, string DisplayName, DateTime SinceUtc);

/// <summary>How the signed-in user relates to another user (see Domain.RelationshipStatus), plus counts for the profile page.</summary>
public sealed record ConnectionStatusDto(string Status, int MutualConnectionCount, int ConnectionCount);

/// <summary>"People you may know": a 2nd-degree contact and how many connections we share.</summary>
public sealed record ConnectionSuggestionDto(Guid UserId, string DisplayName, int MutualConnectionCount);

/// <summary>Body of POST /api/connections/degrees.</summary>
public sealed record NetworkDegreesRequest(IReadOnlyList<Guid> UserIds);

/// <summary>How far a user is from the caller: 1 = connected, 2 = connected through someone, null = not in the network.</summary>
public sealed record NetworkDegreeDto(Guid UserId, int? Degree, int MutualConnectionCount);

/// <summary>Graph result for one suggestion before names are added.</summary>
public sealed record SuggestionEdge(Guid UserId, int MutualConnectionCount);

/// <summary>A user id and timestamp read from the graph; names are added from UserService.</summary>
public sealed record ConnectionEdge(Guid UserId, DateTime SinceUtc);
