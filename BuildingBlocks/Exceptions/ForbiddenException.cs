namespace BuildingBlocks.Exceptions;

/// <summary>
/// The authenticated user is not allowed to perform this action (not the owner, wrong user type).
/// Mapped to HTTP 403 by GlobalExceptionMiddleware.
/// </summary>
public sealed class ForbiddenException(string message) : Exception(message);
