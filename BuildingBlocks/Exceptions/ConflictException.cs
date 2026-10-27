namespace BuildingBlocks.Exceptions;

/// <summary>
/// A business rule rejected the request because of the current state of the data
/// (duplicate, wrong status, already decided). Mapped to HTTP 409 by GlobalExceptionMiddleware.
/// </summary>
public sealed class ConflictException(string message) : Exception(message);
