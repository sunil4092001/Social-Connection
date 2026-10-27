namespace BuildingBlocks.Events;

/// <summary>Published by ConnectionService when a user sends a connection request. Recipient: the addressee.</summary>
public sealed record ConnectionRequestedEvent(
    Guid EventId,
    Guid RequesterUserId,
    Guid AddresseeUserId,
    string RequesterName,
    DateTime OccurredAtUtc);

/// <summary>Published by ConnectionService when the addressee accepts. Recipient: the original requester.</summary>
public sealed record ConnectionAcceptedEvent(
    Guid EventId,
    Guid RequesterUserId,
    Guid AddresseeUserId,
    string AddresseeName,
    DateTime OccurredAtUtc);
