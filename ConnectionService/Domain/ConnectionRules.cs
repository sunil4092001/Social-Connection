using BuildingBlocks.Exceptions;

namespace ConnectionService.Domain;

/// <summary>Status of a REQUESTED relationship in the graph. Missing status (older data) means PENDING.</summary>
public static class RequestStatus
{
    public const string Pending = "PENDING";
    public const string Rejected = "REJECTED";
}

/// <summary>How the signed-in user relates to another user, as shown on a profile page.</summary>
public static class RelationshipStatus
{
    public const string Self = "SELF";
    public const string None = "NONE";
    public const string PendingSent = "PENDING_SENT";
    public const string PendingReceived = "PENDING_RECEIVED";
    public const string Connected = "CONNECTED";
}

/// <summary>What the graph says about two users at the moment a request is sent (read inside the write transaction).</summary>
/// <param name="Connected">They are already connected.</param>
/// <param name="MyRequestStatus">Status of my existing request to them, or null when there is none.</param>
/// <param name="TheyRequested">They already have a pending request to me.</param>
public sealed record ConnectionState(bool Connected, string? MyRequestStatus, bool TheyRequested);

/// <summary>
/// Rules for sending a connection request. A connection is a mutual relationship that needs the other
/// user's consent; it is separate from a professional's interest in a requirement.
/// </summary>
public static class ConnectionRules
{
    public static void EnsureCanRequest(ConnectionState state)
    {
        if (state.Connected)
            throw new ConflictException("You are already connected with this user.");
        if (state.MyRequestStatus == RequestStatus.Pending)
            throw new ConflictException("You have already sent a connection request to this user.");
        if (state.TheyRequested)
            throw new ConflictException("This user has already sent you a connection request. Accept it from your connection requests.");
        // A previously REJECTED request may be sent again; it becomes PENDING.
    }
}
